using UnityEditor;
using UnityEngine;
using Unity.Pipeline.Models;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Telemetry;
using Unity.Pipeline.Threading;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace Unity.Pipeline.Editor
{
    /// <summary>
    /// Automatically starts Pipeline HTTP server when Unity Editor loads.
    /// Handles startup, domain reload persistence, and cleanup.
    ///
    /// Static owner of the live editor server: a static owner survives domain reloads cleanly
    /// (re-created by [InitializeOnLoad]), whereas a ScriptableObject's lifetime does not track the
    /// server across editor events. <see cref="EditorPipelineConfig"/> holds the optional authored
    /// settings (ProjectSettings/, never an asset), read here at start.
    /// </summary>
    [InitializeOnLoad]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    static class PipelineServerStartup
    {
        private static EditorPipelineServer m_Server;

        /// <summary>
        /// The live editor pipeline server instance (null when stopped). Exposed so the test guard
        /// can disable its watchdog for a test run and the EditorPipelineConfig inspector can read
        /// live status.
        /// </summary>
        public static EditorPipelineServer Server => m_Server;

        static PipelineServerStartup()
        {
            // Seed the compile service's define snapshot while we're guaranteed on the main
            // thread — a background compile fired before any main-thread parse would otherwise
            // run with an empty define set (wrong #if branches). Before the worker-process
            // early-out: costs one editor API call and holds everywhere.
            Compilation.RoslynCompilationService.SnapshotProjectDefines();

            // Don't start server in AssetImportWorker processes
            if (!EditorProcessInfo.IsMainProcess())
                return;
            // Setup command discovery using TypeCache for fast Editor performance
            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());

            // Tell a project still carrying the pre-AUTHAPI-66 settings asset, once, that it is inert.
            EditorPipelineLegacyAssetNotice.WarnOncePerSession();

            // Reconcile Application.runInBackground against the recorded override state (CLI-1432).
            // Must not depend on the server being started below (AutoStart can be false) — both the
            // command override and the Play-mode setting apply regardless of whether the server is
            // running. isPlayingOrWillChangePlaymode (not isPlaying) treats "on our way into Play
            // mode" as playing too, so a domain reload mid-entry doesn't see playing=false and
            // wrongly restore; on an ordinary fresh Editor load it is false, so this call doubles as
            // crash recovery — an Editor that died mid-Play with the override recorded restores the
            // project's own value here instead of leaving it stuck on.
            ReconcileRunInBackground(EditorApplication.isPlayingOrWillChangePlaymode);

            var cfg = EditorPipelineConfig.Load();
            bool autoStart;
            try
            {
                // Apply the local eval-usage telemetry config on EVERY domain reload, not only when
                // the server starts (AUTHAPI-29): recording hooks eval itself, not the server
                // lifecycle, so an AutoStart=false session must still honor the configured opt-outs
                // instead of falling back to the hardcoded defaults.
                ApplyEvalTelemetrySettings(cfg);
                autoStart = cfg?.AutoStart ?? EditorPipelineConfig.DefaultAutoStart;
            }
            finally
            {
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }

            // Start the pipeline server (respecting the authored autoStart if settings exist)
            if (autoStart)
                StartServer();

            // Handle domain reloads and editor shutdown
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.wantsToQuit += OnEditorWantsToQuit;

            // Handle domain reload detection
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        /// <summary>Start the server if it isn't already running.</summary>
        public static void EnsureServerStarted()
        {
            StartServer();
        }

        /// <summary>
        /// Force a clean restart of the editor pipeline server. Unlike EnsureServerStarted, this
        /// works even when the current server's listener has died but still reports IsRunning
        /// (e.g. after a test disrupted it). Used by tests to revive the live server they disrupted.
        /// </summary>
        public static void RestartServer()
        {
            StopServer();
            StartServer();
        }

        [MenuItem("Window/Pipeline/Start Server")]
        private static void MenuStartServer()
        {
            StartServer();
            if (m_Server != null && m_Server.IsRunning)
                Debug.Log($"Pipeline Server started on port {m_Server.Port}");
            else
                Debug.LogWarning("Pipeline Server failed to start");
        }

        [MenuItem("Window/Pipeline/Start Server", true)]
        private static bool MenuStartServerValidate() => m_Server == null || !m_Server.IsRunning;

        [MenuItem("Window/Pipeline/Stop Server")]
        private static void MenuStopServer()
        {
            StopServer();
        }

        [MenuItem("Window/Pipeline/Stop Server", true)]
        private static bool MenuStopServerValidate() => m_Server != null && m_Server.IsRunning;


        /// <summary>
        /// Apply the local eval-usage telemetry config (AUTHAPI-29). Local-only, no transmission;
        /// disabling it stops even local recording. StoreSource is the explicit opt-in for
        /// persisting raw eval source (off by default). Resolving the directory primes the
        /// dataPath-derived default on the main thread, so off-main-thread readers (report_evals)
        /// and the background record task never touch Unity APIs.
        ///
        /// Internal (not private): this is the ONE place that knows how to turn the authored settings
        /// into the static telemetry config, so every push site — the domain-reload static ctor,
        /// server start, and <see cref="EditorPipelineConfigEditor"/>'s live inspector push — calls
        /// through here rather than re-deriving the two-field mapping independently, which would
        /// silently drift the moment a fourth push site (or a third telemetry field) showed up.
        /// </summary>
        internal static void ApplyEvalTelemetrySettings(EditorPipelineConfig cfg)
        {
            EvalUsageTelemetry.Enabled = cfg?.EvalTelemetryEnabled ?? EditorPipelineConfig.DefaultEvalTelemetryEnabled;
            EvalUsageTelemetry.StoreSource = cfg?.StoreEvalSource ?? EditorPipelineConfig.DefaultStoreEvalSource;
            EvalUsageTelemetry.ResolveDirectory();
        }

        /// <summary>
        /// Push every setting that takes effect without a restart — the watchdog, request logging
        /// and the eval-telemetry statics — onto the live server. Port and AutoStart are absent on
        /// purpose: they only apply at the next start.
        ///
        /// Both surfaces that can change these values go through here: the settings page's
        /// interactive edits, and its reload when the file changed underneath it (a hand edit, a
        /// second editor, or the leftover-asset notice's copy button). Reloading used to refresh
        /// only what was displayed, leaving the running server on the old values until the next GUI
        /// edit or restart.
        /// </summary>
        internal static void ApplyLiveSettings(EditorPipelineConfig cfg)
        {
            var server = m_Server;
            if (server != null)
            {
                server.WatchdogEnabled = cfg?.WatchdogEnabled ?? EditorPipelineConfig.DefaultWatchdogEnabled;
                server.WatchdogIntervalSeconds = cfg?.WatchdogIntervalSeconds ?? EditorPipelineConfig.DefaultWatchdogIntervalSeconds;
                server.LogRequestsResponses = cfg?.LogRequestsResponses ?? EditorPipelineConfig.DefaultLogRequestsResponses;
            }

            ApplyEvalTelemetrySettings(cfg);
        }

        /// <summary>
        /// Start the Pipeline HTTP server, reading the authored EditorPipelineConfig settings if any
        /// exist (otherwise using defaults).
        /// </summary>
        private static void StartServer()
        {
            if (m_Server != null && m_Server.IsRunning)
                return;

            var cfg = EditorPipelineConfig.Load();
            try
            {
                m_Server = new EditorPipelineServer
                {
                    // Only the settings that cannot change without a restart. Everything that applies
                    // live comes from ApplyLiveSettings below, so the fallbacks for those fields exist
                    // in exactly one place.
                    AllowSandboxedBrowserClients = cfg?.AllowBrowserClients ?? EditorPipelineConfig.DefaultAllowBrowserClients,
                    MaxRequestBodyBytes = 32L * 1024 * 1024
                };

                // Self-healing watchdog: if the listener dies without a Stop() (an unexpected fault
                // outside a domain reload), the watchdog re-opens it so the dogfood loop doesn't
                // wedge. Must run before Start(), which arms the watchdog. Also re-applies the
                // eval-telemetry statics on every server start, so edits made this session stick
                // even without a domain reload (the static ctor covers the reload path).
                ApplyLiveSettings(cfg);

                // Rotate the transaction log once per Unity session (main thread; SessionState-gated).
                // The append path runs off-thread and can't touch SessionState.
                PipelineTransactionLog.RotateForNewSession();

                m_Server.Start(cfg?.Port ?? EditorPipelineConfig.DefaultPort); // 0 auto-assigns from the 7800-7849 range.

                // Restore whatever auto-tick state the user last set this session (survives the
                // domain reload that just wiped AutoTickCommand's statics). Only a session with no
                // prior explicit set_autotick call falls back to a default, and that default is "on"
                // when the watchdog is enabled: the watchdog rides EditorApplication.update, but a
                // backgrounded/idle editor stops ticking once the listener dies (no requests left to
                // wake it) — the exact moment the watchdog must run. Keeping auto-tick on by default
                // keeps the update loop spinning regardless of focus, which keeps both the watchdog
                // AND the dispatcher message pump alive.
                Commands.AutoTickCommand.RestoreFromSession(defaultEnabled: m_Server.WatchdogEnabled);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to start Pipeline Server: {ex.Message}");
            }
            finally
            {
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// Stop the Pipeline HTTP server.
        /// </summary>
        public static void StopServer()
        {
            if (m_Server != null)
            {
                try
                {
                    m_Server.Stop();
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Error stopping Pipeline Server: {ex.Message}");
                }
                finally
                {
                    m_Server = null;
                }
            }
        }

        /// <summary>
        /// Handle play mode state changes.
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Apply/restore Application.runInBackground for the recorded override state (CLI-1432):
            // ExitingEditMode fires when entering Play mode (before any domain reload that causes);
            // EnteredEditMode fires when a Play session ends.
            if (state == PlayModeStateChange.ExitingEditMode)
                ReconcileRunInBackground(playing: true);
            else if (state == PlayModeStateChange.EnteredEditMode)
                ReconcileRunInBackground(playing: false);

            // Server continues running through play mode changes
            // Status endpoint will reflect current play mode via EditorApplication.isPlaying

            // RuntimePipelineDriver.Awake() switches CommandRegistry to reflection-based discovery
            // on every Play Mode entry (a Player has no TypeCache). With Fast Enter Play Mode
            // (domain reload disabled), nothing else runs this static constructor to switch it back
            // on the way out — pre-FEPM, re-entering Edit mode via a domain reload did this for
            // free. Restore TypeCache discovery explicitly here instead of relying on that reload.
            if (state == PlayModeStateChange.EnteredEditMode)
                CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());

            // Dynamic command registrations (CommandRegistry.RegisterCommand) are static state with
            // domain-reload lifetime, and a script registering from Awake/OnEnable re-registers on
            // every Play Mode entry. With domain reload disabled nothing resets the registry between
            // sessions, so the second entry would throw "already registered dynamically" and a
            // surviving entry would hold a delegate bound to the previous session's destroyed
            // object. Bracket the session here to clear what it registered; registrations made in
            // Edit Mode are kept, since nothing re-runs to restore them.
            if (state == PlayModeStateChange.ExitingEditMode)
                CommandRegistry.PlayModeSessionStarted();
            else if (state == PlayModeStateChange.EnteredEditMode)
                CommandRegistry.PlayModeSessionEnded();
        }

        /// <summary>SessionState flag: a build put the project's Run In Background value back and has
        /// not yet re-applied the override (see <see cref="BuildProcessors.PipelineRuntimeBuildProcessor"/>).</summary>
        internal const string RunInBackgroundRestoredForBuildKey = "Unity.Pipeline.RunInBackground.RestoredForBuild";

        /// <summary>
        /// Reconciles <see cref="Application.runInBackground"/> against the recorded override state
        /// (CLI-1432): a standing override from <c>editor.playmode set_run_in_background</c> (persists across
        /// Editor restarts until reverted), OR the "Ensure editor runs in background in PlayMode"
        /// project setting (<see cref="EditorPipelineConfig.RunInBackgroundInPlayMode"/>), which only
        /// applies while <paramref name="playing"/> is true.
        ///
        /// Loads its own <see cref="EditorPipelineConfig"/> instance (or a default one when no
        /// settings file exists yet), reconciles it, and saves ONLY when the override record itself
        /// changed — so calling this on every domain reload and Play mode transition never authors a
        /// settings file out of nothing.
        ///
        /// Call sites: <see cref="OnPlayModeStateChanged"/> (Play session start/end), the static
        /// constructor (also crash recovery — an Editor that died mid-Play with the override recorded
        /// restores the project's own value on the next load instead of leaving it stuck on), and
        /// <see cref="BuildProcessors.PipelineRuntimeBuildProcessor"/> (re-applying the override once
        /// a build finishes). The settings page and <c>editor.playmode set_run_in_background</c> call the
        /// <see cref="ReconcileRunInBackground(EditorPipelineConfig, bool, out string)"/> overload
        /// instead, against their own already-loaded config instance.
        /// </summary>
        internal static void ReconcileRunInBackground(bool playing)
        {
            var cfg = EditorPipelineConfig.Load() ?? ScriptableObject.CreateInstance<EditorPipelineConfig>();
            try
            {
                if (ReconcileRunInBackground(cfg, playing, out _))
                    cfg.Save();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>
        /// Same reconciliation as <see cref="ReconcileRunInBackground(bool)"/>, applied in place to
        /// an already-loaded <paramref name="cfg"/> instance rather than one this method loads and
        /// owns. Used by the settings page (which must keep reconciling against its OWN cached
        /// instance, never a second independently-loaded one, to avoid racing its own
        /// out-of-band-write detection) and by <c>editor.playmode set_run_in_background</c> (which has its own
        /// record field to persist regardless of whether the override state itself changed, so it
        /// saves unconditionally rather than relying on this method's return value).
        ///
        /// Mutates <paramref name="cfg"/> and <see cref="Application.runInBackground"/> in memory
        /// only — never writes to disk; the caller owns persistence.
        /// </summary>
        /// <returns>
        /// Whether the override record (<see cref="EditorPipelineConfig.RunInBackgroundOverridden"/> /
        /// <see cref="EditorPipelineConfig.RunInBackgroundOriginal"/>) changed, i.e. whether the
        /// caller has something worth saving.
        /// </returns>
        internal static bool ReconcileRunInBackground(EditorPipelineConfig cfg, bool playing, out string status)
        {
            // While our override is active the live value is always true, so a live false is a change
            // made underneath us. Two causes: a build put the project's value back and never got its
            // post-process (failed/cancelled build; the session flag says so), or the user turned it
            // off themselves, in which case the user wins and nothing is re-applied in this pass.
            // The flag is SessionState, so it dies with the Editor: after a restart a leftover false
            // is indistinguishable from the user's choice and is treated as theirs.
            bool restoredForBuild = SessionState.GetBool(RunInBackgroundRestoredForBuildKey, false);
            if (restoredForBuild)
                SessionState.SetBool(RunInBackgroundRestoredForBuildKey, false);

            if (cfg.RunInBackgroundOverridden && !Application.runInBackground)
            {
                if (!restoredForBuild)
                {
                    AdoptUserRunInBackground(cfg);
                    status = "Run In Background is off: it was turned off while the override was active, " +
                        "so that value wins and the override has ended. Run editor.playmode set_run_in_background " +
                        "again to override it.";
                    return true;
                }

                // The build's doing, not the user's: re-apply, then reconcile as usual.
                Application.runInBackground = true;
            }

            bool desired = cfg.RunInBackgroundCommandOverride || (cfg.RunInBackgroundInPlayMode && playing);
            bool changed = false;

            if (desired && !cfg.RunInBackgroundOverridden)
            {
                cfg.SetRunInBackgroundOverrideRecord(overridden: true, original: Application.runInBackground);
                Application.runInBackground = true;
                changed = true;
            }
            else if (!desired && cfg.RunInBackgroundOverridden)
            {
                Application.runInBackground = cfg.RunInBackgroundOriginal;
                cfg.SetRunInBackgroundOverrideRecord(overridden: false, original: cfg.RunInBackgroundOriginal);
                changed = true;
            }

            status = DescribeRunInBackground(cfg, playing);
            return changed;
        }

        /// <summary>
        /// The user changed Run In Background themselves while the override was active (the live
        /// value is false, which the override never leaves it at): their value becomes the new
        /// original, and both the override and the command override end. Mutates <paramref name="cfg"/>
        /// only; the caller saves.
        /// </summary>
        internal static void AdoptUserRunInBackground(EditorPipelineConfig cfg)
        {
            cfg.SetRunInBackgroundOverrideRecord(overridden: false, original: Application.runInBackground);
            cfg.SetRunInBackgroundCommandOverride(false);
        }

        /// <summary>
        /// Human-readable summary of the current Run In Background state, for
        /// <c>editor.playmode set_run_in_background</c>'s result string.
        /// </summary>
        private static string DescribeRunInBackground(EditorPipelineConfig cfg, bool playing)
        {
            string state = Application.runInBackground ? "on" : "off";

            if (!cfg.RunInBackgroundOverridden)
                return $"Run In Background is {state} (the project's own value; nothing is overriding it)";

            string original = cfg.RunInBackgroundOriginal ? "on" : "off";
            if (!cfg.RunInBackgroundCommandOverride && cfg.RunInBackgroundInPlayMode && playing)
            {
                return $"Run In Background is {state} (overriding the project's value, {original} — " +
                    "the \"Ensure editor runs in background in PlayMode\" setting is still forcing it " +
                    "while in Play mode; builds use the project's value)";
            }

            return $"Run In Background is {state} (overriding the project's value, {original}; " +
                "builds use the project's value)";
        }

        /// <summary>
        /// Handle Editor shutdown.
        /// </summary>
        private static void OnEditorQuitting()
        {
            StopServer(); // Stop() shuts down the server's own dispatcher.
        }

        private static bool OnEditorWantsToQuit()
        {
            // WARNING: we need to fire SessionStop in WantsToQuit. Firing in Quits won't actually send the event.
            PipelineAnalytics.SendSessionStoppedIfStarted();
            return true;
        }

        /// <summary>
        /// Handle before assembly reload (domain reload).
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            // Server will be automatically recreated after reload due to [InitializeOnLoad]
            // Instance descriptor file will be cleaned up and recreated

        }

        /// <summary>
        /// Handle after assembly reload (domain reload).
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Server should already be restarted via [InitializeOnLoad]
            // This is mainly for logging/verification
        }
    }
}

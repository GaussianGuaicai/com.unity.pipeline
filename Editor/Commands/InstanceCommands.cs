using System.Collections.Generic;
using System.Linq;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Models;
using UnityEngine;

namespace Unity.Pipeline.Editor.Commands
{
    static class InstanceCommands
    {
        // Grouped under editor.playmode, alongside the scenario commands and the core
        // play/stop/pause controls. The command name is derived from the method name.
        const string Group = "editor.playmode";

        [CliGroupCommand(Group,
            "List running Editor instances reachable over Pipeline (this Editor + its MPPM virtual player clones). Connect to one with 'unity command --project-path <its projectPath>'.",
            MainThreadRequired = false,
            Tags = new[] { "editor/instances" })]
        public static object ListInstances()
        {
            var live = ReadLiveInstances(ProjectRoot());
            return new
            {
                count = live.Count,
                instances = live
                    .OrderBy(d => d.Role == MppmInstanceInfo.RoleMain ? 0 : 1)
                    .ThenBy(d => d.Port)
                    .Select(d => new
                    {
                        name = d.InstanceName,
                        role = d.Role,
                        port = d.Port,
                        pid = d.Pid,
                        vpId = d.VpId,
                        projectPath = d.ProjectPath
                    })
                    .ToList()
            };
        }

        internal static List<EditorInstanceDescriptor> LiveInstances()
        {
            return ReadLiveInstances(ProjectRoot());
        }

        private static string ProjectRoot()
        {
            return System.IO.Path.GetDirectoryName(Application.dataPath);
        }

        private static List<EditorInstanceDescriptor> ReadLiveInstances(string mainProjectPath)
        {
            return EditorInstanceDescriptor.DiscoverAll(mainProjectPath)
                .Where(d => d != null && d.Port > 0 && IsWriterAlive(d))
                .ToList();
        }

        internal static bool IsWriterAlive(EditorInstanceDescriptor descriptor)
        {
            if (descriptor.Pid <= 0)
                return false;

            try
            {
                var process = System.Diagnostics.Process.GetProcessById(descriptor.Pid);
                if (process.HasExited)
                    return false;

                if (descriptor.StartedAt == default)
                    return true;

                // A crashed clone leaves its descriptor behind and the OS can hand its PID to another
                // process, even another Editor. The writer started before it stamped StartedAt, so a
                // process that started after that is a later one reusing the PID.
                var stampedAt = descriptor.StartedAt.Kind == System.DateTimeKind.Local
                    ? descriptor.StartedAt.ToUniversalTime()
                    : System.DateTime.SpecifyKind(descriptor.StartedAt, System.DateTimeKind.Utc);
                return process.StartTime.ToUniversalTime() <= stampedAt.AddSeconds(1);
            }
            catch
            {
                return false;
            }
        }
    }
}

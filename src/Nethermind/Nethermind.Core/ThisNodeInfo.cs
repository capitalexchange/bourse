// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace Nethermind.Core
{
    public static class ThisNodeInfo
    {
        private static readonly ConcurrentDictionary<string, string> _nodeInfoItems = new();

        public static void AddInfo(string infoDescription, string value) => _nodeInfoItems.TryAdd(infoDescription, value);

        public static string BuildNodeInfoScreen()
        {
            StringBuilder builder = new();
            builder.AppendLine();
            builder.AppendLine(BourseBanner);
            builder.AppendLine("-----------------------------  Initialization Completed  -----------------------------");
            builder.AppendLine();

            foreach ((string key, string value) in _nodeInfoItems.OrderByDescending(static ni => ni.Key))
            {
                builder.AppendLine($"{key} {value}");
            }

            builder.Append("--------------------------------------------------------------------------------------");
            return builder.ToString();
        }

        /// <summary>
        /// The Bourse fork's startup banner. Replaces the upstream Nethermind ASCII-art logo
        /// with a single concise line plus this fork's version.
        /// </summary>
        private static string BourseBanner =>
            $"BOURSE by Capital Exchange Markets | Digital - hardforked Nethermind (v{ProductInfo.Version})";
    }
}

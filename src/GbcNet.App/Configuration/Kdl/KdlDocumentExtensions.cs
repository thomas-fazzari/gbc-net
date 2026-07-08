// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using KdlSharp;
using KdlSharp.Extensions;

namespace GbcNet.App.Configuration.Kdl;

/// <summary>
/// Configuration-specific extensions for top-level KDL sections.
/// </summary>
internal static class KdlDocumentExtensions
{
    extension(KdlDocument document)
    {
        public KdlNode? ReadOptionalSection(string nodeName)
        {
            using var nodes = document.FindNodes(nodeName).GetEnumerator();
            if (!nodes.MoveNext())
            {
                return null;
            }

            var section = nodes.Current;
            return nodes.MoveNext()
                ? throw new ConfigurationException(
                    $"Config file must contain only one {nodeName} node."
                )
                : section;
        }

        public KdlNode ReadRequiredSection(string nodeName) =>
            document.ReadOptionalSection(nodeName)
            ?? throw new ConfigurationException($"Config file must contain one {nodeName} node.");
    }
}

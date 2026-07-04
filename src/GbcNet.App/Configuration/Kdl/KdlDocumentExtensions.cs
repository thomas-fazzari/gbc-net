// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;
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
        public Result<KdlNode?> ReadOptionalSection(string nodeName)
        {
            using var nodes = document.FindNodes(nodeName).GetEnumerator();
            if (!nodes.MoveNext())
            {
                return Result.Ok<KdlNode?>(null);
            }

            var section = nodes.Current;
            return nodes.MoveNext()
                ? Result.Fail($"Config file must contain only one {nodeName} node.")
                : Result.Ok<KdlNode?>(section);
        }

        public Result<KdlNode> ReadRequiredSection(string nodeName)
        {
            var section = document.ReadOptionalSection(nodeName);
            if (section.IsFailed)
            {
                return section.ToResult<KdlNode>();
            }

            return section.Value is null
                ? Result.Fail($"Config file must contain one {nodeName} node.")
                : Result.Ok(section.Value);
        }
    }
}

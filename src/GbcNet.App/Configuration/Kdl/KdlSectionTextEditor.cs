// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;
using KdlSharp;
using KdlSharp.Parsing;

namespace GbcNet.App.Configuration.Kdl;

internal static class KdlSectionTextEditor
{
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';

    public static Result<string> ReplaceTopLevelSection(
        string text,
        KdlNode? section,
        string replacement
    )
    {
        var replacementText = EndsWithLineBreak(replacement)
            ? replacement
            : string.Concat(replacement, LineFeed);

        if (section is null)
        {
            return text.Length == 0 || EndsWithLineBreak(text)
                ? string.Concat(text, replacementText)
                : string.Concat(text, LineFeed, replacementText);
        }

        if (section.SourcePosition is null)
        {
            return Result.Fail($"Config node '{section.Name}' does not have source position.");
        }

        var range = FindSectionRange(text, GetTextIndex(text, section.SourcePosition));
        return string.Concat(text.AsSpan(0, range.Start), replacementText, text.AsSpan(range.End));
    }

    private static SectionRange FindSectionRange(string text, int nodeStart)
    {
        var start = FindLineStart(text, nodeStart);
        using StringReader stringReader = new(text);
        using KdlReader reader = new(stringReader);

        while (reader.Read())
        {
            if (reader.Position < nodeStart)
            {
                continue;
            }

            if (reader.TokenType is KdlTokenType.Newline or KdlTokenType.Semicolon)
            {
                return new SectionRange(start, FindLineEnd(text, reader.Position));
            }

            if (reader.TokenType is KdlTokenType.OpenBrace)
            {
                return new SectionRange(start, FindBlockEnd(text, reader));
            }
        }

        return new SectionRange(start, text.Length);
    }

    private static int FindBlockEnd(string text, KdlReader reader)
    {
        var depth = 1;
        while (reader.Read())
        {
            if (reader.TokenType is KdlTokenType.OpenBrace)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is not KdlTokenType.CloseBrace)
            {
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return IncludeTrailingLineBreak(text, reader.Position + 1);
            }
        }

        return text.Length;
    }

    private static int GetTextIndex(string text, SourcePosition position)
    {
        var line = 1;
        var column = 1;

        var index = 0;
        while (index < text.Length)
        {
            if (line == position.Line && column == position.Column)
            {
                return index;
            }

            if (IsLineBreak(text[index]))
            {
                index = SkipLineBreak(text, index);
                line++;
                column = 1;
                continue;
            }

            column++;
            index++;
        }

        return text.Length;
    }

    private static int FindLineStart(string text, int index)
    {
        while (index > 0 && !IsLineBreak(text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static int FindLineEnd(string text, int index)
    {
        while (index < text.Length && !IsLineBreak(text[index]))
        {
            index++;
        }

        return IncludeTrailingLineBreak(text, index);
    }

    private static int IncludeTrailingLineBreak(string text, int index)
    {
        return SkipLineBreak(text, index);
    }

    private static int SkipLineBreak(string text, int index)
    {
        if (index >= text.Length)
        {
            return index;
        }

        if (text[index] != CarriageReturn)
        {
            return text[index] == LineFeed ? index + 1 : index;
        }

        index++;

        return index < text.Length && text[index] == LineFeed ? index + 1 : index;
    }

    private static bool EndsWithLineBreak(string text) => text.Length > 0 && IsLineBreak(text[^1]);

    private static bool IsLineBreak(char value) => value is CarriageReturn or LineFeed;

    private readonly record struct SectionRange(int Start, int End);
}

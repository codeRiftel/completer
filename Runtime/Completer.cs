using System;
using System.Text;
using System.Collections.Generic;
using option;

namespace completer {
    public class CommandInfo {
        public List<string> flags;
        public List<string> orderedParams;
        public Dictionary<string, List<string>> namedParameters;

        public CommandInfo() {
            flags = new List<string>();
            orderedParams = new List<string>();
            namedParameters = new Dictionary<string, List<string>>();
        }
    }

    public enum PartType {
        Identifier,
        Dash,
        DoubleDash,
        EqualSign
    }

    public struct CommandPart {
        public PartType type;
        public string value;
    }

    public class BoiledCommand {
        public string name;
        public Dictionary<string, string> namedParams;
        public List<string> orderedParams;
        public List<string> flags;

        public BoiledCommand() {
            namedParams = new Dictionary<string, string>();
            orderedParams = new List<string>();
            flags = new List<string>();
        }
    }

    public struct CompletionRes {
        public List<string> completions;
        public int commonLength;

        public static CompletionRes Mk(List<string> completions, int commonLength) {
            return new CompletionRes { completions = completions, commonLength = commonLength };
        }
    }

    public enum ParseErr {
        None,
        EmptyCommand,
        MustStartWithCommand,
        WrongHyphenUse,
        ParamExpected,
        ExpectedEqualSign,
        FlagCannotBeQuoted,
        ParamNameCannotBeQuoted,
        ExpectedParameterValue,
        EqualSignOutOfPlace,
        UnknownToken
    }

    enum Token {
        Unknown,
        Identifier,
        Hyphen,
        EqualSign,
        EOF
    }

    struct LexRes {
        public Token token;
        public int start;
        public int length;

        public static LexRes Unknown() {
            return new LexRes { token = Token.Unknown };
        }

        public static LexRes Identifier(int start, int length) {
            return new LexRes { token = Token.Identifier, start = start, length = length };
        }

        public static LexRes Hyphen(int start) {
            return new LexRes { token = Token.Hyphen, start = start, length = 1 };
        }

        public static LexRes Equals(int start) {
            return new LexRes { token = Token.EqualSign, start = start, length = 1 };
        }

        public static LexRes EOF() {
            return new LexRes { token = Token.EOF };
        }
    }

    public static class Completer {
        public static CompletionRes GetCompletions(
            Dictionary<string, CommandInfo> commandsInfo,
            string line
        ) {
            var completions = new List<string>();

            var pos = 0;
            LexRes lexRes = new LexRes();

            var lexResults = new List<LexRes>();

            var first = true;
            var commandName = "";
            while (lexRes.token != Token.EOF) {
                lexRes = Lex(line, pos);
                if (first) {
                    first = false;
                    if (lexRes.token == Token.Identifier) {
                        commandName = line.Substring(lexRes.start, lexRes.length);
                    } else {
                        return CompletionRes.Mk(new List<string>(commandsInfo.Keys), 0);
                    }
                }

                lexResults.Add(lexRes);

                pos = lexRes.start + lexRes.length;
            }

            CommandInfo info = null;
            if (!commandsInfo.ContainsKey(commandName)) {
                if (line.Length == 0) {
                    foreach (var possibleCmd in commandsInfo.Keys) {
                        if (possibleCmd.StartsWith(commandName)) {
                            completions.Add(possibleCmd);
                        }
                    }
                }
                return CompletionRes.Mk(completions, commandName.Length);
            } else {
                info = commandsInfo[commandName];
            }

            if (lexResults.Count <= 1) {
                return CompletionRes.Mk(completions, 0);
            }

            var part = "";
            var prevPart = "";
            var prevPrevPart = "";

            Token token = Token.Unknown;
            Token prevToken = Token.Unknown;
            Token prevPrevToken = Token.Unknown;

            if (lexResults.Count > 1) {
                var targetLexRes = lexResults[lexResults.Count - 2];
                part = line.Substring(targetLexRes.start, targetLexRes.length);
                token = targetLexRes.token;
            }

            if (lexResults.Count > 2) {
                var targetLexRes = lexResults[lexResults.Count - 3];
                prevPart = line.Substring(targetLexRes.start, targetLexRes.length);
                prevToken = targetLexRes.token;
            }

            if (lexResults.Count > 3) {
                var targetLexRes = lexResults[lexResults.Count - 4];
                prevPrevPart = line.Substring(targetLexRes.start, targetLexRes.length);
                prevPrevToken = targetLexRes.token;
            }

            int commonLength = 0;
            if (token == Token.Identifier) {
                if (prevToken == Token.EqualSign) {
                    if (prevPrevToken == Token.Identifier) {
                        if (info.namedParameters.ContainsKey(prevPrevPart)) {
                            var possibleValues = info.namedParameters[prevPrevPart];
                            foreach (var possible in possibleValues) {
                                if (possible.StartsWith(part)) {
                                    completions.Add(possible);
                                }
                            }
                            commonLength = part.Length;
                        }
                    }
                } else if (prevToken == Token.Hyphen) {
                    if (prevPrevToken != Token.Hyphen) {
                        foreach (var flag in info.flags) {
                            if (flag.StartsWith(part) && part != flag) {
                                completions.Add(flag);
                            }
                        }
                        commonLength = part.Length;
                    } else {
                        foreach (var named in info.namedParameters) {
                            var name = named.Key;
                            if (name.StartsWith(part) && part != name) {
                                completions.Add(name);
                            }
                        }
                        commonLength = part.Length;
                    }
                } else {
                    foreach (var ordered in info.orderedParams) {
                        if (ordered.StartsWith(part) && part != ordered) {
                            completions.Add(ordered);
                        }
                    }
                    commonLength = part.Length;
                }
            } else if (token == Token.EqualSign) {
                if (prevToken == Token.Identifier) {
                    if (info.namedParameters.ContainsKey(prevPart)) {
                        completions.AddRange(info.namedParameters[prevPart]);
                    }
                }
            } else if (token == Token.Hyphen) {
                if (prevToken == Token.Hyphen) {
                    foreach (var namedParam in info.namedParameters) {
                        completions.Add(namedParam.Key);
                    }
                } else {
                    completions.AddRange(info.flags);
                }
            } else {
                completions.AddRange(info.orderedParams);
            }


            return CompletionRes.Mk(completions, commonLength);
        }

        public static Result<BoiledCommand, ParseErr> ParseCommand(string line) {
            var boiledCommand = new BoiledCommand();

            var pos = 0;
            LexRes lexRes = new LexRes();

            bool first = true;
            while (lexRes.token != Token.EOF) {
                lexRes = Lex(line, pos);

                if (first) {
                    first = false;
                    if (lexRes.token != Token.Identifier) {
                        return Result<BoiledCommand, ParseErr>.Err(ParseErr.MustStartWithCommand);
                    } else {
                        boiledCommand.name = line.Substring(lexRes.start, lexRes.length);
                        pos = lexRes.start + lexRes.length;
                        continue;
                    }
                }

                if (lexRes.token == Token.Hyphen) {
                    pos = lexRes.start + lexRes.length;
                    lexRes = Lex(line, pos);

                    if (lexRes.token != Token.Hyphen && lexRes.token != Token.Identifier) {
                        return Result<BoiledCommand, ParseErr>.Err(ParseErr.WrongHyphenUse);
                    }

                    if (lexRes.token == Token.Hyphen) {
                        pos = lexRes.start + lexRes.length;
                        lexRes = Lex(line, pos);
                        if (lexRes.token != Token.Identifier) {
                            return Result<BoiledCommand, ParseErr>.Err(ParseErr.ParamExpected);
                        }

                        var paramName = line.Substring(lexRes.start, lexRes.length);

                        if (paramName.StartsWith("\"")) {
                            return Result<BoiledCommand, ParseErr>.Err(
                                ParseErr.ParamNameCannotBeQuoted
                            );
                        }

                        pos = lexRes.start + lexRes.length;
                        lexRes = Lex(line, pos);
                        if (lexRes.token != Token.EqualSign) {
                            return Result<BoiledCommand, ParseErr>.Err(
                                ParseErr.ExpectedEqualSign
                            );
                        }

                        pos = lexRes.start + lexRes.length;
                        lexRes = Lex(line, pos);
                        if (lexRes.token != Token.Identifier) {
                            return Result<BoiledCommand, ParseErr>.Err(
                                ParseErr.ExpectedParameterValue
                            );
                        }

                        var paramVal = line.Substring(lexRes.start, lexRes.length);
                        paramVal = NormalizeIdentifier(paramVal);
                        boiledCommand.namedParams[paramName] = paramVal;

                        pos = lexRes.start + lexRes.length;
                    } else if (lexRes.token == Token.Identifier) {
                        var flagName = line.Substring(lexRes.start, lexRes.length);
                        if (flagName.StartsWith("\"")) {
                            return Result<BoiledCommand, ParseErr>.Err(
                                ParseErr.FlagCannotBeQuoted
                            );
                        }

                        boiledCommand.flags.Add(flagName);
                        pos = lexRes.start + lexRes.length;
                    }
                } else if (lexRes.token == Token.Identifier) {
                    var identifier = line.Substring(lexRes.start, lexRes.length);
                    identifier = NormalizeIdentifier(identifier);
                    boiledCommand.orderedParams.Add(identifier);
                    pos = lexRes.start + lexRes.length;
                } else if (lexRes.token == Token.EqualSign) {
                    return Result<BoiledCommand, ParseErr>.Err(ParseErr.EqualSignOutOfPlace);
                } else if (lexRes.token == Token.Unknown) {
                    return Result<BoiledCommand, ParseErr>.Err(ParseErr.UnknownToken);
                }
            }

            return Result<BoiledCommand, ParseErr>.Ok(boiledCommand);
        }

        public static string GenerateCommand(BoiledCommand command) {
            StringBuilder builder = new StringBuilder();
            builder.Append(command.name);

            foreach (var flag in command.flags) {
                builder.Append(" -");
                builder.Append(flag);
            }

            foreach (var paramPair in command.namedParams) {
                builder.Append(" --");
                builder.Append(paramPair.Key);
                builder.Append('=');
                builder.Append(Escape(paramPair.Value));
            }

            foreach (var orderedParam in command.orderedParams) {
                builder.Append(' ');
                builder.Append(Escape(orderedParam));
            }

            return builder.ToString();
        }

        private static string Escape(string str) {
            var quote = false;
            if (str.Contains("\"")) {
                str = str.Replace("\"", "\\\"");
                quote = true;
            }

            quote = quote || str.Contains(" ");

            if (quote) {
                str = $"\"{str}\"";
            }

            return str;
        }

        private static string NormalizeIdentifier(string identifier) {
            if (identifier.StartsWith("\"")) {
                identifier = identifier.Substring(1, identifier.Length - 2);
                identifier = identifier.Replace("\\\"", "\"");
            }

            return identifier;
        }

        private static LexRes Lex(string data, int start) {
            if (start >= data.Length) {
                return LexRes.EOF();
            }

            var pos = start;
            while (IsWhiteSpace(data[pos])) {
                pos++;

                if (pos >= data.Length) {
                    return LexRes.EOF();
                }
            }

            switch (data[pos]) {
                case '=':
                    return LexRes.Equals(pos);
                case '"':
                    var startStr = pos;
                    pos++;
                    if (pos >= data.Length) {
                        return LexRes.EOF();
                    }

                    var pass = false;
                    while (pass || data[pos] != '"') {
                        pass = false;
                        pos++;

                        if (pos >= data.Length) {
                            return LexRes.EOF();
                        }

                        if (data[pos] == '"') {
                            if (data[pos - 1] == '\\') {
                                pass = true;
                            }
                        }
                    }

                    return LexRes.Identifier(startStr, pos - startStr + 1);
                case '-':
                    return LexRes.Hyphen(pos);
                default:
                    var startIdent = pos;

                    while (!IsSpecial(data[pos]) && !IsWhiteSpace(data[pos])) {
                        pos++;

                        if (pos >= data.Length) {
                            break;
                        }
                    }

                    return LexRes.Identifier(startIdent, pos - startIdent);
            }
        }

        private static bool IsSpecial(char c) {
            return c == '=' || c == '"' || c == '-';
        }

        private static bool IsWhiteSpace(char c) {
            return c == ' ' || c == '\t' || c == '\r' || c == '\n';
        }
    }
}

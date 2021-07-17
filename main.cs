using System;
using System.Collections;
using System.Collections.Generic;
using completer;

class Init {
    private static int Main(string[] args) {
        var lines = new List<string>();
        lines.Add("create_cube -r --name=cube --pos=\"0.0 0.0 0.0\"");
        lines.Add("--param=hey create_cube");
        lines.Add("create_sphere --radius=10.0 --name=\"super \\\"sphere\\\"\"");
        lines.Add("create_world");
        lines.Add("create_cylinder 0.0 0.0 0.0 3.0 10.0");
        lines.Add("create_something --");
        lines.Add("create_something --=");
        lines.Add("create_something --a");
        lines.Add("create_something -");
        lines.Add("create_something -f");
        lines.Add("create_something --id=");
        lines.Add("create");
        lines.Add("create_c");
        lines.Add("create_cylinder --identity=fo");
        lines.Add("create_world re");
        lines.Add("create_sphere --i");

        var info = new CommandsInfo();
        info.commands.Add("create_sphere");
        info.commands.Add("create_cube");
        info.commands.Add("create_world");
        info.commands.Add("create_cylinder");
        info.commands.Add("create_something");

        info.flags.Add("foo");
        info.flags.Add("bar");

        info.namedParameters["id"] = new List<string>();
        info.namedParameters["id"].Add("unknown");
        info.namedParameters["id"].Add("01");
        info.namedParameters["id"].Add("99");

        info.namedParameters["identity"] = new List<string>();
        info.namedParameters["identity"].Add("me");
        info.namedParameters["identity"].Add("him");
        info.namedParameters["identity"].Add("foo");
        info.namedParameters["identity"].Add("foooooo");

        info.namedParameters["whatever"] = new List<string>();

        info.orderedParams.Add("red");
        info.orderedParams.Add("reddy");
        info.orderedParams.Add("green");
        info.orderedParams.Add("greeny");

        for (int i = 0; i < lines.Count; i++) {
            TestParser(i, lines[i]);
            TestCompleter(i, lines[i], info);
        }

        return 0;
    }

    private static void TestCompleter(int index, string line, CommandsInfo info) {
        Console.WriteLine();
        Console.WriteLine($"COMPLETER: {index}) {line}");
        var completions = Completer.GetCompletions(info, line);
        foreach (var completion in completions) {
            Console.WriteLine($"=> {completion}");
        }
    }

    private static void TestParser(int index, string line) {
        Console.WriteLine();
        Console.WriteLine($"PARSER: {index}) {line}");
        var res = Completer.ParseCommand(line);
        if (res.IsErr()) {
            var err = res.AsErr();
            Console.WriteLine($"ERROR: {err}");
        } else {
            var recreatedCommand = Completer.GenerateCommand(res.AsOk());
            Console.WriteLine($"recreated: {recreatedCommand}");
        }
    }
}

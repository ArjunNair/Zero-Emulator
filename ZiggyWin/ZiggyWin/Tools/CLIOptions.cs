
using System.Collections.Generic;
using CommandLine;

namespace ZeroWin.Tools
{
    class CLIOptions
    {
        [Option('f', Required = false)]
        public bool Fullscreen { get; set; }

        [Option('m', Separator = ' ', Required = false)]
        public string Machine { get; set; }

        [Option('e', Separator = ' ', Required = false)]
        public int EmulationSpeed { get; set; }

        [Option('c', Separator = ' ', Required = false)]
        public int CPUMultiplier { get; set; }

        [Option('s', Required = false)]
        public bool PixelSmoothing { get; set; }

        [Option('v', Required = false)]
        public bool Vsync { get; set; }

        [Option('p', Separator = ' ', Required = false)]
        public string Palette { get; set; }

        [Option('i', Required = false)]
        public bool Interlaced { get; set; }

        [Option('g', Required = false)]
        public bool UseGDI { get; set; }

        [Option('l', Required = false)]
        public bool LateTimings { get; set; }

        [Option('w', Separator = ' ', Required = false)]
        public int WindowSize { get; set; }

        [Option('b', Separator = ' ', Required = false)]
        public string BorderSize { get; set; }

        [Option('q', Separator = ' ', Required = false)]
        public IEnumerable<string> Queue { get; set; }

    }
}

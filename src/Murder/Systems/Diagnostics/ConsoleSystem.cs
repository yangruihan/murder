﻿using Bang.Contexts;
using Bang.Systems;
using Murder.Core.Graphics;
using Murder.Diagnostics;
using Murder.Editor.Attributes;

namespace Murder.Systems
{
    [DoNotPause]
    [OnlyShowOnDebugView]
    [Filter(ContextAccessorFilter.None)]
    public class ConsoleSystem : IStartupSystem, IGuiSystem
    {
        private GameLogger _logger = null!;

        public void Start(Context context)
        {
            _logger = GameLogger.GetOrCreateInstance();
        }

        public void DrawGui(RenderContext render, Context context)
        {
            // Outside of the game, also display the console.
            _logger.DrawConsole((string input) =>
            {
                return CommandServices.Parse(context.World, input);
            });
        }
    }
}
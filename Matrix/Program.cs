﻿using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace Matrix
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>() // This refers to the App class from App.xaml.cs
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}

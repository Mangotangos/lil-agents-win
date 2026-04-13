// Explicit aliases to resolve WPF vs WinForms namespace conflicts.
// Both are needed: WinForms for NotifyIcon, WPF for everything else.

global using Application          = System.Windows.Application;
global using Brush                 = System.Windows.Media.Brush;
global using Brushes               = System.Windows.Media.Brushes;
global using Color                 = System.Windows.Media.Color;
global using HorizontalAlignment   = System.Windows.HorizontalAlignment;
global using Key                   = System.Windows.Input.Key;
global using KeyEventArgs          = System.Windows.Input.KeyEventArgs;
global using MouseButtonEventArgs  = System.Windows.Input.MouseButtonEventArgs;
global using SolidColorBrush       = System.Windows.Media.SolidColorBrush;

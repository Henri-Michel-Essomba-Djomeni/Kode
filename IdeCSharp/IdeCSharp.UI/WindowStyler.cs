using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IdeCSharp.UI
{
    /// <summary>Petits réglages de fenêtre que WPF ne propose pas directement.</summary>
    internal static class WindowStyler
    {
        // Attribut de Windows : barre de titre en mode sombre (Windows 10 2004 et plus)
        private const int DwmUseImmersiveDarkMode = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void SetDarkTitleBar(Window window, bool dark)
        {
            var handle = new WindowInteropHelper(window).EnsureHandle();
            int value = dark ? 1 : 0;
            // Si Windows ne connaît pas l'attribut, l'appel échoue sans conséquence
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        }
    }
}
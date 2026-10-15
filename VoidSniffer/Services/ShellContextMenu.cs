using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Shows the standard Windows shell context menu (the same one Explorer shows) for a
    /// file or folder path, by hosting the <c>IContextMenu</c> shell interfaces.
    /// </summary>
    public static class ShellContextMenu
    {
        /// <summary>Shows the shell context menu for <paramref name="path"/> in screen coordinates.</summary>
        public static bool Show(IntPtr ownerHwnd, string path, int screenX, int screenY)
        {
            if (string.IsNullOrEmpty(path)) return false;

            IntPtr contextMenuPtr = IntPtr.Zero;
            IntPtr hMenu = IntPtr.Zero;
            IShellItem item = null;
            IContextMenu contextMenu = null;
            var filter = new MenuMessageFilter();
            bool filterAdded = false;

            try
            {
                Guid shellItemIid = typeof(IShellItem).GUID;
                int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref shellItemIid, out item);
                if (hr != 0 || item == null) return false;

                Guid sfuiObject = BHID_SFUIObject;
                Guid contextMenuIid = typeof(IContextMenu).GUID;
                uint bindHr = item.BindToHandler(IntPtr.Zero, ref sfuiObject, ref contextMenuIid, out contextMenuPtr);
                if (bindHr != 0 || contextMenuPtr == IntPtr.Zero) return false;

                contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPtr);

                IContextMenu2 contextMenu2 = null;
                IContextMenu3 contextMenu3 = null;
                try { contextMenu2 = contextMenu as IContextMenu2; } catch (InvalidCastException) { }
                try { contextMenu3 = contextMenu as IContextMenu3; } catch (InvalidCastException) { }

                hMenu = CreatePopupMenu();
                if (hMenu == IntPtr.Zero) return false;

                const uint CMF_NORMAL = 0;
                const uint idCmdFirst = 1;
                const uint idCmdLast = 0x7FFF;
                contextMenu.QueryContextMenu(hMenu, 0, idCmdFirst, idCmdLast, CMF_NORMAL);

                if (contextMenu2 != null || contextMenu3 != null)
                {
                    filter.ContextMenu2 = contextMenu2;
                    filter.ContextMenu3 = contextMenu3;
                    Application.AddMessageFilter(filter);
                    filterAdded = true;
                }

                const uint TPM_RETURNCMD = 0x0100;
                const uint TPM_RIGHTBUTTON = 0x0002;
                int command = TrackPopupMenuEx(
                    hMenu, TPM_RETURNCMD | TPM_RIGHTBUTTON, screenX, screenY, ownerHwnd, IntPtr.Zero);

                if (command > 0)
                {
                    var info = new CMINVOKECOMMANDINFOEX();
                    info.cbSize = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX));
                    info.fMask = CMIC_MASK_UNICODE;
                    info.hwnd = ownerHwnd;
                    info.lpVerb = (IntPtr)(command - idCmdFirst);
                    info.lpVerbW = (IntPtr)(command - idCmdFirst);
                    info.nShow = SW_SHOWNORMAL;
                    contextMenu.InvokeCommand(ref info);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (filterAdded) Application.RemoveMessageFilter(filter);
                if (hMenu != IntPtr.Zero) DestroyMenu(hMenu);
                if (contextMenu != null) Marshal.ReleaseComObject(contextMenu);
                if (contextMenuPtr != IntPtr.Zero) Marshal.Release(contextMenuPtr);
                if (item != null) Marshal.ReleaseComObject(item);
            }
        }

        private static readonly Guid BHID_SFUIObject = new Guid("3981E225-F559-11D3-8E3A-00C04F6837D5");
        private const uint CMIC_MASK_UNICODE = 0x00004000;
        private const int SW_SHOWNORMAL = 1;

        /// <summary>Forwards menu messages to the shell so owner-draw and submenus work.</summary>
        private sealed class MenuMessageFilter : IMessageFilter
        {
            public IContextMenu2 ContextMenu2 { get; set; }
            public IContextMenu3 ContextMenu3 { get; set; }

            public bool PreFilterMessage(ref Message m)
            {
                const int WM_DRAWITEM = 0x002B;
                const int WM_MEASUREITEM = 0x002C;
                const int WM_INITMENUPOPUP = 0x0117;
                const int WM_MENUCHAR = 0x0120;

                switch (m.Msg)
                {
                    case WM_DRAWITEM:
                    case WM_MEASUREITEM:
                    case WM_INITMENUPOPUP:
                        if (ContextMenu2 != null)
                        {
                            ContextMenu2.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam);
                            return true;
                        }
                        if (ContextMenu3 != null)
                        {
                            ContextMenu3.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam);
                            return true;
                        }
                        break;

                    case WM_MENUCHAR:
                        if (ContextMenu3 != null)
                        {
                            IntPtr result;
                            ContextMenu3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, out result);
                            m.Result = result;
                            return true;
                        }
                        break;
                }
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CMINVOKECOMMANDINFOEX
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public IntPtr lpParameters;
            public IntPtr lpDirectory;
            public int nShow;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr lpTitle;
            public IntPtr lpVerbW;
            public IntPtr lpParametersW;
            public IntPtr lpDirectoryW;
            public IntPtr lpTitleW;
            public POINT ptInvoke;
        }

        [ComImport]
        [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig]
            uint BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, out IntPtr ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport]
        [Guid("000214E4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu
        {
            void QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            void GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, IntPtr pszName, uint cchMax);
        }

        [ComImport]
        [Guid("000214F4-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu2
        {
            void QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            void GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, IntPtr pszName, uint cchMax);
            void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport]
        [Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IContextMenu3
        {
            void QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            void InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
            void GetCommandString(UIntPtr idCmd, uint uFlags, IntPtr pReserved, IntPtr pszName, uint cchMax);
            void HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
            void HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);
    }
}

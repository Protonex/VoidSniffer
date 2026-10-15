using System;
using System.Collections.Generic;
using VoidSniffer.Model;

namespace VoidSniffer.Services
{
    /// <summary>
    /// Owns the treemap navigation state: the current root, the focused node and the
    /// back/forward history. UI-agnostic so it can be unit tested without WinForms.
    /// </summary>
    public sealed class NavigationController
    {
        private readonly Stack<FileNode> _back = new Stack<FileNode>();
        private readonly Stack<FileNode> _forward = new Stack<FileNode>();

        private FileNode _root;
        private FileNode _focus;

        /// <summary>Raised whenever <see cref="Focus"/> or <see cref="Root"/> changes.</summary>
        public event EventHandler FocusChanged;

        public FileNode Root
        {
            get { return _root; }
        }

        public FileNode Focus
        {
            get { return _focus; }
        }

        public bool CanGoBack
        {
            get { return _back.Count > 0; }
        }

        public bool CanGoForward
        {
            get { return _forward.Count > 0; }
        }

        public bool CanZoomUp
        {
            get { return _focus != null && _focus.Parent != null; }
        }

        public bool IsAtRoot
        {
            get { return _root != null && ReferenceEquals(_focus, _root); }
        }

        public int BackDepth
        {
            get { return _back.Count; }
        }

        public int ForwardDepth
        {
            get { return _forward.Count; }
        }

        /// <summary>Replaces the whole tree and resets the history to its root.</summary>
        public void SetRoot(FileNode root)
        {
            _root = root;
            _focus = root;
            _back.Clear();
            _forward.Clear();
            RaiseFocusChanged();
        }

        /// <summary>Focuses a folder. Non-folder nodes and the current focus are ignored.</summary>
        public void NavigateTo(FileNode node)
        {
            if (node == null || !node.IsFolder) return;
            if (ReferenceEquals(node, _focus)) return;

            if (_focus != null)
            {
                _back.Push(_focus);
            }
            _forward.Clear();
            _focus = node;
            RaiseFocusChanged();
        }

        /// <summary>Moves the focus to the parent folder.</summary>
        public void ZoomUp()
        {
            if (CanZoomUp)
            {
                NavigateTo(_focus.Parent);
            }
        }

        /// <summary>Moves the focus back to the tree root.</summary>
        public void GoHome()
        {
            if (_root != null)
            {
                NavigateTo(_root);
            }
        }

        public void GoBack()
        {
            if (_back.Count == 0) return;

            if (_focus != null)
            {
                _forward.Push(_focus);
            }
            _focus = _back.Pop();
            RaiseFocusChanged();
        }

        public void GoForward()
        {
            if (_forward.Count == 0) return;

            if (_focus != null)
            {
                _back.Push(_focus);
            }
            _focus = _forward.Pop();
            RaiseFocusChanged();
        }

        public void Reset()
        {
            _root = null;
            _focus = null;
            _back.Clear();
            _forward.Clear();
            RaiseFocusChanged();
        }

        private void RaiseFocusChanged()
        {
            EventHandler handler = FocusChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}

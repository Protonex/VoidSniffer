using System;
using VoidSniffer.Model;
using VoidSniffer.Services;
using Xunit;

namespace VoidSniffer.Tests
{
    public class NavigationControllerTests
    {
        private static FileNode Root(string name, params FileNode[] children)
        {
            return TestTree.Folder(name, children);
        }

        [Fact]
        public void SetRoot_FocusesRootAndClearsHistory()
        {
            var nav = new NavigationController();
            nav.SetRoot(Root("a"));

            Assert.NotNull(nav.Root);
            Assert.Same(nav.Root, nav.Focus);
            Assert.False(nav.CanGoBack);
            Assert.False(nav.CanGoForward);
            Assert.True(nav.IsAtRoot);
        }

        [Fact]
        public void NavigateTo_PushesBackAndSupportsForward()
        {
            FileNode child = TestTree.Folder("child");
            FileNode root = Root("root", child);

            var nav = new NavigationController();
            nav.SetRoot(root);
            nav.NavigateTo(child);

            Assert.Same(child, nav.Focus);
            Assert.True(nav.CanGoBack);

            nav.GoBack();
            Assert.Same(root, nav.Focus);
            Assert.True(nav.CanGoForward);

            nav.GoForward();
            Assert.Same(child, nav.Focus);
        }

        [Fact]
        public void NavigateTo_IgnoresFiles()
        {
            FileNode file = TestTree.File("f.txt", 10);
            FileNode root = Root("root", file);

            var nav = new NavigationController();
            nav.SetRoot(root);
            nav.NavigateTo(file);

            Assert.Same(root, nav.Focus);
        }

        [Fact]
        public void RebuildRoot_PreservesFocusAndHistoryByPath()
        {
            FileNode child = TestTree.Folder("child");
            FileNode grand = TestTree.Folder("grand");
            child.Children.Add(grand);
            FileNode root = Root("root", child);

            var nav = new NavigationController();
            nav.SetRoot(root);
            nav.NavigateTo(child);
            nav.NavigateTo(grand);

            // Rebuild with brand-new node instances but identical paths.
            FileNode newGrand = TestTree.Folder("grand");
            FileNode newChild = TestTree.Folder("child");
            newChild.Children.Add(newGrand);
            FileNode newRoot = Root("root", newChild);

            nav.RebuildRoot(newRoot);

            Assert.Same(newRoot, nav.Root);
            Assert.Equal("grand", nav.Focus.Name);
            Assert.Same(newGrand, nav.Focus);
            Assert.True(nav.CanGoBack);

            nav.GoBack();
            Assert.Same(newChild, nav.Focus);
        }

        [Fact]
        public void ZoomUp_MovesFocusToParent()
        {
            FileNode child = TestTree.Folder("child");
            FileNode root = Root("root", child);

            var nav = new NavigationController();
            nav.SetRoot(root);
            nav.NavigateTo(child);
            nav.ZoomUp();

            Assert.Same(root, nav.Focus);
        }
    }
}

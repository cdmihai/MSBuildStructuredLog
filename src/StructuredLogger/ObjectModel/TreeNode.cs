﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public abstract class TreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool isVisible = true;
        public bool IsVisible
        {
            get
            {
                return isVisible;
            }

            set
            {
                if (isVisible == value)
                {
                    return;
                }

                isVisible = value;
                RaisePropertyChanged();
            }
        }

        private bool isSelected = false;
        public bool IsSelected
        {
            get
            {
                return isSelected;
            }

            set
            {
                if (isSelected == value)
                {
                    return;
                }

                isSelected = value;
                RaisePropertyChanged();
                RaisePropertyChanged("IsLowRelevance");
            }
        }

        private bool isExpanded = false;
        public bool IsExpanded
        {
            get
            {
                return isExpanded;
            }

            set
            {
                if (isExpanded == value)
                {
                    return;
                }

                isExpanded = value;
                RaisePropertyChanged();
            }
        }

        private bool isLowRelevance = false;
        public bool IsLowRelevance
        {
            get
            {
                return isLowRelevance && !IsSelected;
            }

            set
            {
                if (isLowRelevance == value)
                {
                    return;
                }

                isLowRelevance = value;
                RaisePropertyChanged();
            }
        }

        public TreeNode Parent { get; set; }

        public IEnumerable<TreeNode> GetParentChain()
        {
            var chain = new List<TreeNode>();
            TreeNode current = this;
            while (current.Parent != null)
            {
                current = current.Parent as TreeNode;
                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }

        private ObservableCollection<object> children;
        public ObservableCollection<object> Children
        {
            get
            {
                if (children == null)
                {
                    children = new ObservableCollection<object>();
                    children.CollectionChanged += Children_CollectionChanged;
                }

                return children;
            }
        }

        public bool HasChildren { get; private set; } = false;

        public virtual void AddChild(TreeNode child)
        {
            Children.Add(child);
            child.Parent = this;
        }

        public NamedNode GetOrCreateNodeWithName<T>(string name) where T : NamedNode, new()
        {
            var existing = FindChild<T>(n => n.Name == name);
            if (existing == null)
            {
                existing = new T() { Name = name };
                this.AddChild(existing);
            }

            return existing;
        }

        private void Children_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            bool oldHasChildren = HasChildren;
            HasChildren = children != null && children.Count > 0;
            if (HasChildren != oldHasChildren)
            {
                RaisePropertyChanged(nameof(HasChildren));
            }
        }

        public virtual T FindChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<T>())
                {
                    if (predicate == null || predicate(child))
                    {
                        return child;
                    }
                }
            }

            return default(T);
        }

        public virtual T FindFirstInSubtree<T>(Predicate<T> predicate = null)
        {
            if (this is T && (predicate == null || predicate((T)(object)this)))
            {
                return (T)(object)this;
            }

            return FindFirstChild<T>(predicate);
        }

        public virtual T FindFirstChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    var found = child.FindFirstInSubtree<T>(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return default(T);
        }

        public virtual T FindLastInSubtree<T>(Predicate<T> predicate = null)
        {
            var child = FindLastChild<T>(predicate);
            if (child != null)
            {
                return child;
            }

            if (this is T && (predicate == null || predicate((T)(object)this)))
            {
                return (T)(object)this;
            }

            return default(T);
        }

        public virtual T FindLastChild<T>(Predicate<T> predicate = null)
        {
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>().Reverse())
                {
                    var found = child.FindLastInSubtree<T>(predicate);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return default(T);
        }

        public int FindIndex(TreeNode child)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] == child)
                {
                    return i;
                }
            }

            return -1;
        }

        public T FindPrevious<T>(TreeNode child, Predicate<T> predicate = null)
        {
            var i = FindIndex(child);
            if (i == -1)
            {
                return default(T);
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (Children[j] is T && (predicate == null || predicate((T)Children[j])))
                {
                    return (T)Children[j];
                }
            }

            return default(T);
        }

        public T FindNext<T>(TreeNode child, Predicate<T> predicate = null)
        {
            var i = FindIndex(child);
            if (i == -1)
            {
                return default(T);
            }

            for (int j = i + 1; j < Children.Count; j++)
            {
                if (Children[j] is T && (predicate == null || predicate((T)Children[j])))
                {
                    return (T)Children[j];
                }
            }

            return default(T);
        }

        public T FindPrevious<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
            }

            return Parent.FindPrevious<T>(this, predicate);
        }

        public T FindNext<T>(Predicate<T> predicate = null)
        {
            if (Parent == null)
            {
                return default(T);
            }

            return Parent.FindNext<T>(this, predicate);
        }

        public T FindPreviousInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            var current = FindPrevious<TreeNode>() as TreeNode;

            while (current != null)
            {
                var last = current.FindLastInSubtree<T>(predicate);
                if (last != null)
                {
                    return last;
                }

                if (Parent != null)
                {
                    current = Parent.FindPrevious<TreeNode>(current) as TreeNode;
                }
                else
                {
                    return default(T);
                }
            }

            if (Parent != null)
            {
                if (Parent is T && (predicate == null || predicate((T)(object)Parent)))
                {
                    return (T)(object)Parent;
                }

                return Parent.FindPreviousInTraversalOrder<T>(predicate);
            }

            return default(T);
        }

        public T FindNextInTraversalOrder<T>(Predicate<T> predicate = null)
        {
            var current = FindNext<TreeNode>() as TreeNode;

            while (current != null)
            {
                var first = current.FindFirstInSubtree<T>(predicate);
                if (first != null)
                {
                    return first;
                }

                if (Parent != null)
                {
                    current = Parent.FindNext<TreeNode>(current) as TreeNode;
                }
                else
                {
                    return default(T);
                }
            }

            if (Parent != null)
            {
                return Parent.FindNextInTraversalOrder<T>(predicate);
            }

            return default(T);
        }

        public void VisitAllChildren<T>(Action<T> processor)
        {
            if (this is T)
            {
                processor((T)(object)this);
            }

            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    var node = child as TreeNode;
                    if (node != null)
                    {
                        node.VisitAllChildren<T>(processor);
                    }
                }
            }
        }

        public virtual int TotalItemsInSubtree()
        {
            int sum = 1;
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    sum += child.TotalItemsInSubtree();
                }
            }

            return sum;
        }

        public virtual void WriteTo(StringBuilder sb, int indent = 0)
        {
            sb.Append(new string(' ', indent * 4));
            sb.AppendLine(this.ToString());
            if (HasChildren)
            {
                foreach (var child in Children.OfType<TreeNode>())
                {
                    child.WriteTo(sb, indent + 1);
                }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace CollisionQuadTree
{
    public class Entity<T>
    {
        private Rect rect;

        public Rect Rect
        {
            get => rect;
            set
            {
                if (rect.Equals(value)) return;
                rect = value;
                Dirty = true;
            }
        }

        public T Item { get; private set; }

        private bool _dirty;
        public bool Dirty
        {
            get => _dirty;
            internal set
            {
                if (_dirty == value) return;
                _dirty = value;
                // owner节点脏标记
                if (value)
                    foreach (var owner in Owners)
                        owner?.MarkDirty();
            }
        }

        public bool Removed { get; private set; }
        /// <summary>
        /// 属于哪些节点
        /// </summary>
        internal HashSet<TreeNode<T>> Owners { get; }

        public Entity(Rect rect, T item)
        {
            Owners = new HashSet<TreeNode<T>>();
            Set(rect, item);
        }

        public void Set(Rect rect, T item)
        {
            this.rect = rect;
            Item = item;
            Dirty = false;
            Removed = false;
            Owners.Clear();
        }

        public void Reset()
        {
            Rect = new Rect();
            Item = default;
            Dirty = false;
            Removed = false;
            Owners.Clear();
        }
        
        public bool Overlaps(Rect rect)
        {
            return Rect.Overlaps(rect);
        }

        public bool Query(Rect queryRect)
        {
            return !Removed && Overlaps(queryRect);
        }

        public void MarkRemove()
        {
            Removed = true;
            Dirty = true;
        }
        
        internal void ClearDirty()
        {
            Dirty = false;
        }
        
        internal void AddOwner(TreeNode<T> owner)
        {
            Owners.Add(owner);
            if (Dirty)
                owner.MarkDirty();
        }
        
        internal void RemoveOwner(TreeNode<T> owner)
        {
            Owners.Remove(owner);
        }

        internal void ClearOwners()
        {
            Owners.Clear();
        }
    }
}

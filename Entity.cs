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
                if (Rect.Equals(value)) return;
                Rect = rect;
                Dirty = true;
            }
        }

        public T Item { get; private set; }
        internal bool Dirty { get; private set; }
        internal bool Removed { get; private set; }
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
        }

        internal void ResetDirty()
        {
            Dirty = false;
        }
        
        internal void AddOwner(TreeNode<T> owner)
        {
            Owners.Add(owner);
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

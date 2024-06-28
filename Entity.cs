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
        public bool Dirty { get; internal set; }
        public bool Removed { get; set; }
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

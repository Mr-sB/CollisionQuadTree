using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace CollisionQuadTree
{
    public class TreeNode<T> : IEnumerable<TreeNode<T>>
    {
        public const int MaxChildrenCount = 4;
        public QuadTree<T> Tree { get; private set; }
        public TreeNode<T> Parent { get; private set; }
        public Rect Rect { get; private set; }
        public int Depth { get; private set; }
        /// <summary>
        /// 只有非叶子节点才持有Child
        /// </summary>
        public TreeNode<T>[] Children { get; private set; }
        /// <summary>
        /// 只有叶子节点才持有Entity
        /// </summary>
        public HashSet<Entity<T>> Entities { get; }
        /// <summary>
        /// 是否是叶子节点
        /// </summary>
        public bool IsLeaf { get; private set; }
        public bool NeedSplit => IsLeaf && Depth < Tree.MaxDepth && Entities.Count >= Tree.MaxItemCount;
        /// <summary>
        /// 是否所有子节点都是叶子节点
        /// </summary>
        public bool AllChildrenAreLeaves
        {
            get
            {
                if (IsLeaf) return false;
                // 只要其中一个child不是叶子节点，则false
                foreach (var child in Children)
                    if (!child.IsLeaf) return false;
                return true;
            }
        }

        public int AllEntitiesCount
        {
            get
            {
                int count = 0;
                if (IsLeaf)
                    count += Entities.Count;
                else
                {
                    foreach (var child in Children)
                        count += child.AllEntitiesCount;
                }
                return count;
            }
        }
        
        public TreeNode(QuadTree<T> tree, TreeNode<T> parent, Rect rect, int depth)
        {
            Children = null;
            Entities = new HashSet<Entity<T>>();
            Set(tree, parent, rect, depth);
        }

        public void Set(QuadTree<T> tree, TreeNode<T> parent, Rect rect, int depth)
        {
            Tree = tree;
            Parent = parent;
            Rect = rect;
            Depth = depth;
            Entities.Clear();
            IsLeaf = true;
        }

        public void Reset()
        {
            Tree = null;
            Parent = null;
            Rect = new Rect();
            Depth = 0;
            Entities.Clear();
            if (!IsLeaf)
                foreach (var child in Children)
                    child.Reset();
            IsLeaf = true;
        }

        public bool Overlaps(Rect rect)
        {
            return Rect.Overlaps(rect);
        }
        
        public void Add(Entity<T> entity)
        {
            // 不在范围内，不需要添加
            if (!Rect.Overlaps(entity.Rect)) return;
            if (NeedSplit)
                Split();
            if (IsLeaf)
            {
                // 叶子节点，直接添加
                Entities.Add(entity);
                entity.ResetDirty();
                entity.AddOwner(this);
            }
            else
            {
                foreach (var child in Children)
                {
                    // 如果一个实体跨越了多个象限，那么添加到所有跨越的象限中
                    if (child.Overlaps(entity.Rect))
                        child.Add(entity);
                }
            }
        }

        public void MarkRemove(T item)
        {
            foreach (var node in this)
            {
                if (!node.IsLeaf) continue;
                foreach (var entity in node.Entities)
                {
                    if (EqualityComparer<T>.Default.Equals(entity.Item, item))
                        entity.MarkRemove();
                }
            }
        }

        internal bool Remove(Entity<T> entity)
        {
            if (!IsLeaf) return false;
            return Entities.Remove(entity);
        }
        
        public void Query(Rect queryRect, [NotNull] List<Entity<T>> results)
        {
            // 不在范围内，不需要添加
            if (!Rect.Overlaps(queryRect)) return;

            if (IsLeaf)
            {
                foreach (var entity in Entities)
                {
                    if (entity.Query(queryRect))
                        results.Add(entity);
                }
            }
            else
            {
                foreach (var child in Children)
                    child.Query(queryRect, results);
            }
        }

        internal bool Merge()
        {
            if (!AllChildrenAreLeaves) return false;
            
            IsLeaf = true;
            // Children的所有实体重新归纳到当前节点
            foreach (var child in Children)
            {
                foreach (var entity in child.Entities)
                {
                    entity.RemoveOwner(child);
                    Entities.Add(entity);
                    entity.AddOwner(this);
                }
                child.Reset();
            }
            return true;
        }
        
        private void Split()
        {
            if (!IsLeaf) return;
            IsLeaf = false;
            int childDepth = Depth + 1;
            if (Children == null)
            {
                Children = new TreeNode<T>[MaxChildrenCount];
                for (int i = 0; i < MaxChildrenCount; i++)
                    Children[i] = new TreeNode<T>(Tree, this, QuadrantHelper.GetRect(Rect, (QuadrantEnum) i), childDepth);
            }
            else
            {
                for (int i = 0; i < MaxChildrenCount; i++)
                    Children[i].Set(Tree, this, QuadrantHelper.GetRect(Rect, (QuadrantEnum) i), childDepth);
            }
            // 当前的所有实体重新分配到Children中
            foreach (var entity in Entities)
            {
                entity.RemoveOwner(this);
                Add(entity);
            }
            Entities.Clear();
        }

        public IEnumerator<TreeNode<T>> GetEnumerator()
        {
            yield return this;
            if (!IsLeaf)
            {
                foreach (var child in Children)
                {
                    foreach (var node in child)
                        yield return node;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

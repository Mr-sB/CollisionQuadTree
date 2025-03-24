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
        public bool NeedSplit => IsLeaf && Depth < Tree.MaxDepth && Entities.Count >= Tree.MaxItemCount;
        public bool NeedMerge => AllChildrenAreLeaves && AllEntitiesCount <= Tree.MaxItemCount;
        public bool Dirty { get; private set; }

        
        internal TreeNode(QuadTree<T> tree, TreeNode<T> parent, Rect rect, int depth)
        {
            Children = null;
            Entities = new HashSet<Entity<T>>();
            Set(tree, parent, rect, depth);
        }

        internal void Set(QuadTree<T> tree, TreeNode<T> parent, Rect rect, int depth)
        {
            Tree = tree;
            Parent = parent;
            Rect = rect;
            Depth = depth;
            Entities.Clear();
            IsLeaf = true;
        }

        internal void Reset()
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

        internal bool Overlaps(Rect rect)
        {
            return Rect.Overlaps(rect);
        }
        
        internal bool Add(Entity<T> entity)
        {
            // 不在范围内，不需要添加
            if (!Overlaps(entity.Rect)) return false;
            TrySplit();
            if (IsLeaf)
            {
                // 叶子节点，直接添加
                Entities.Add(entity);
                entity.AddOwner(this);
                return true;
            }
            else
            {
                bool added = false;
                foreach (var child in Children)
                {
                    // 如果一个实体跨越了多个象限，那么添加到所有跨越的象限中
                    if (child.Overlaps(entity.Rect))
                        added |= child.Add(entity);
                }
                return added;
            }
        }

        private void ForceAdd(Entity<T> entity)
        {
            TrySplit();
            if (IsLeaf)
            {
                // 叶子节点，直接添加
                Entities.Add(entity);
                entity.AddOwner(this);
            }
            else
            {
                bool added = false;
                foreach (var child in Children)
                {
                    // 如果一个实体跨越了多个象限，那么添加到所有跨越的象限中
                    if (child.Overlaps(entity.Rect))
                        added |= child.Add(entity);
                }
                // 强制添加到最后
                if (!added)
                    Children[Children.Length - 1].ForceAdd(entity);
            }
        }

        internal void MarkRemove(T item)
        {
            foreach (var node in this)
            {
                if (!node.IsLeaf) continue;
                foreach (var entity in node.Entities)
                {
                    if (EqualityComparer<T>.Default.Equals(entity.Item, item))
                    {
                        entity.MarkRemove();
                        return;
                    }
                }
            }
        }

        internal bool Remove(Entity<T> entity)
        {
            if (!IsLeaf) return false;
            return Entities.Remove(entity);
        }
        
        /// <summary>
        /// 查询与哪些entity有交集
        /// </summary>
        /// <param name="queryRect">查询范围</param>
        /// <param name="results">查询结果。因为实体可能存在于多个Node中，所以使用HashSet去重</param>
        internal void Query(Rect queryRect, [NotNull] HashSet<Entity<T>> results)
        {
            // 不在范围内，不需要查询
            if (!Overlaps(queryRect)) return;

            if (IsLeaf)
            {
                foreach (var entity in Entities)
                {
                    // 该实体不存在多个Node中，或者查询结果里没有，则需要查询
                    if ((entity.Owners.Count <= 1 || !results.Contains(entity)) && entity.Query(queryRect))
                        results.Add(entity);
                }
            }
            else
            {
                foreach (var child in Children)
                    child.Query(queryRect, results);
            }
        }

        internal bool TryMerge()
        {
            if (!NeedMerge) return false;
            
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
        
        private bool TrySplit()
        {
            if (!NeedSplit) return false;
            
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
                // 该节点无法处理的实体
                if (!Add(entity))
                {
                    // 再次标脏，避免出错
                    entity.Dirty = true;
                    // 强制添加到最后一个child中
                    Children[Children.Length - 1].ForceAdd(entity);
                }
            }
            Entities.Clear();
            return true;
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

        internal void MarkDirty()
        {
            if (Dirty) return;
            Dirty = true;
            Parent?.MarkDirty();
        }

        internal void ClearDirty()
        {
            Dirty = false;
        }
    }
}

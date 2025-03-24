using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CollisionQuadTree
{
    public class QuadTree<T> : IEnumerable<TreeNode<T>>
    {
        public int MaxDepth { get; private set; }
        public int MaxItemCount { get; private set; }
        public TreeNode<T> Root { get; }
        private HashSet<Entity<T>> allEntities;
        private HashSet<Entity<T>> reInsertEntities;
        private HashSet<Entity<T>> toRemoveEntities; // 使用Set去重，因为实体可能存在于多个Node中
        internal HashSet<Entity<T>> OutsideEntities; // 在Root范围之外的实体

        public QuadTree(int maxDepth, int maxItemCount, Rect rootRect)
        {
            MaxDepth = maxDepth;
            MaxItemCount = maxItemCount;
            Root = new TreeNode<T>(this, null, rootRect, 0);
            allEntities = new HashSet<Entity<T>>();
            reInsertEntities = new HashSet<Entity<T>>();
            toRemoveEntities = new HashSet<Entity<T>>();
            OutsideEntities = new HashSet<Entity<T>>();
        }

        public void Set(int maxDepth, int maxItemCount, Rect rootRect)
        {
            MaxDepth = maxDepth;
            MaxItemCount = maxItemCount;
            Root.Reset();
            Root.Set(this, null, rootRect, 0);
            allEntities.Clear();
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
            OutsideEntities.Clear();
        }

        public void Reset()
        {
            MaxDepth = 0;
            MaxItemCount = 0;
            Root.Reset();
            allEntities.Clear();
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
            OutsideEntities.Clear();
        }
        
        /// <summary>
        /// 刷新整棵树
        /// 具体频率看业务需求。推荐每帧调用，需要立即同步最新数据时调用
        /// </summary>
        public void Update()
        {
            reInsertEntities.Clear();
            toRemoveEntities.Clear();

            // 处理outside的实体
            foreach (var outsideEntity in OutsideEntities)
            {
                if (outsideEntity.Removed)
                {
                    toRemoveEntities.Add(outsideEntity);
                    allEntities.Remove(outsideEntity);
                }
                else if (outsideEntity.Dirty && Root.Overlaps(outsideEntity.Rect))
                {
                    // 在范围内了，可以添加
                    reInsertEntities.Add(outsideEntity);
                    toRemoveEntities.Add(outsideEntity);
                }

                // 如果没有节点持有该实体，去掉脏标记
                // 否则保留，等待PreprocessingEntities继续使用和恢复脏标记
                if (outsideEntity.Owners.Count <= 0)
                    outsideEntity.ClearDirty();
            }
            foreach (var entity in toRemoveEntities)
                OutsideEntities.Remove(entity);
            
            toRemoveEntities.Clear();
            // 预处理实体
            PreprocessingEntities(Root);

            // 处理Removed的实体
            if (toRemoveEntities.Count > 0)
            {
                // 移除Removed的节点
                foreach (var toRemoveEntity in toRemoveEntities)
                {
                    foreach (var node in toRemoveEntity.Owners)
                        node.Remove(toRemoveEntity);
                }
                
                // 合并节点
                foreach (var toRemoveEntity in toRemoveEntities)
                {
                    foreach (var node in toRemoveEntity.Owners)
                    {
                        var iterator = node.Parent;
                        while (iterator != null)
                        {
                            // 尝试合并节点。如果不需要合并节点，直接停止循环
                            if (!iterator.TryMerge()) break;
                            
                            // 合并成功，继续往上判断是否需要合并
                            iterator = iterator.Parent;
                        }
                    }
                    toRemoveEntity.ClearOwners();
                }
            }
            
            // 重新添加之前dirty的实体
            foreach (var entity in reInsertEntities)
                Add(entity);
            
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
        }

        /// <summary>
        /// 添加实体到四叉树。过滤重复添加的实体
        /// </summary>
        /// <param name="entity">实体</param>
        /// <returns>是否添加成功：重复添加返回false</returns>
        public bool Add(Entity<T> entity)
        {
            if (!allEntities.Add(entity))
            {
                // 在管理中的正常实体，不允许重复添加
                if (!entity.Removed) return false;
                // 实体标记为需要移除了，重新添加需要清除移除标记
                // 这种情况算添加成功
                entity.ClearRemove();
                return true;
            }
            if (!Root.Add(entity))
            {
                // 添加的实体不在最大范围内，额外存储
                OutsideEntities.Add(entity);
            }
            return true;
        }

        /// <summary>
        /// 标记移除给定item的实体
        /// 最好直接调用外部持有的Entity.MarkRemove直接标记
        /// </summary>
        /// <param name="item"></param>
        public void MarkRemove(T item)
        {
            foreach (var entity in allEntities)
            {
                if (EqualityComparer<T>.Default.Equals(entity.Item, item))
                {
                    entity.MarkRemove();
                    return;
                }
            }
        }
        
        /// <summary>
        /// 查询与哪些entity有交集
        /// </summary>
        /// <param name="queryRect">查询范围</param>
        /// <param name="results">查询结果。因为实体可能存在于多个Node中，所以使用HashSet去重</param>
        public HashSet<Entity<T>> Query(Rect queryRect, HashSet<Entity<T>> results = null)
        {
            if (results == null)
                results = new HashSet<Entity<T>>();
            else
                results.Clear();

            Root.Query(queryRect, results);
            
            return results;
        }

        public IEnumerator<TreeNode<T>> GetEnumerator()
        {
            return Root.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void PreprocessingEntities(TreeNode<T> parent)
        {
            // 没有脏标记不需要处理了
            if (parent == null || !parent.Dirty) return;
            parent.ClearDirty();
            
            // 记录需要操作的Entities
            foreach (var entity in parent.Entities)
            {
                if (entity.Removed)
                {
                    toRemoveEntities.Add(entity);
                    allEntities.Remove(entity);
                }
                else if (entity.Dirty)
                {
                    // dirty的对象直接标记移除，后续重新添加
                    toRemoveEntities.Add(entity);
                    reInsertEntities.Add(entity);
                }
                
                entity.ClearDirty();
            }
            
            // 递归处理子节点
            if (!parent.IsLeaf)
                foreach (var child in parent.Children)
                    PreprocessingEntities(child);
        }
    }
}

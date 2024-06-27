using System.Collections.Generic;
using UnityEngine;

namespace CollisionQuadTree
{
    public class QuadTree<T>
    {
        public int MaxDepth { get; private set; }
        public int MaxItemCount { get; private set; }
        public TreeNode<T> Root { get; }
        private List<Entity<T>> reInsertEntities;
        private HashSet<Entity<T>> toRemoveEntities; // 使用Set去重，因为实体可能存在于多个Node中

        public QuadTree(int maxDepth, int maxItemCount, Rect rootRect)
        {
            MaxDepth = maxDepth;
            MaxItemCount = maxItemCount;
            Root = new TreeNode<T>(this, null, rootRect, 0);
            reInsertEntities = new List<Entity<T>>();
            toRemoveEntities = new HashSet<Entity<T>>();
        }

        public void Set(int maxDepth, int maxItemCount, Rect rootRect)
        {
            MaxDepth = maxDepth;
            MaxItemCount = maxItemCount;
            Root.Reset();
            Root.Set(this, null, rootRect, 0);
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
        }

        public void Reset()
        {
            MaxDepth = 0;
            MaxItemCount = 0;
            Root.Reset();
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
        }
        
        /// <summary>
        /// 刷新整棵树
        /// 具体频率看业务需求。推荐每帧调用，需要立即同步最新数据时调用
        /// </summary>
        public void Update()
        {
            reInsertEntities.Clear();
            toRemoveEntities.Clear();
            
            // 处理Dirty的实体
            foreach (var node in Root)
            {
                foreach (var entity in node.Entities)
                {
                    // dirty的对象直接标记移除，后续重新添加
                    if (!entity.Removed && entity.Dirty)
                    {
                        entity.MarkRemove();
                        reInsertEntities.Add(entity);
                    }
                    // 使用Set去重，因为实体可能存在于多个Node中
                    if (entity.Removed)
                        toRemoveEntities.Add(entity);
                }
            }

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
                        // 所有子节点都是叶子节点才有合并的条件
                        while (iterator != null && iterator.AllChildrenAreLeaves)
                        {
                            // 达到分层条件，不需要合并节点，直接停止循环
                            if (iterator.AllEntitiesCount > MaxItemCount)
                                break;
                            
                            // 合并节点
                            if (!iterator.Merge())
                                break;
                            // 合并成功
                            // 继续往上判断是否需要合并
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

        public void Add(Entity<T> entity)
        {
            Root.Add(entity);
        }

        /// <summary>
        /// 标记移除给定item的实体
        /// 最好直接调用外部持有的Entity.MarkRemove方法
        /// </summary>
        /// <param name="item"></param>
        public void MarkRemove(T item)
        {
            Root.MarkRemove(item);
        }

        public List<Entity<T>> Query(Rect queryRect, List<Entity<T>> results = null)
        {
            if (results == null)
                results = new List<Entity<T>>();
            else
                results.Clear();

            Root.Query(queryRect, results);
            
            return results;
        }
    }
}

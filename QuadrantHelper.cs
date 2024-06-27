using UnityEngine;

namespace CollisionQuadTree
{
    public static class QuadrantHelper
    {
        public static Rect GetRect(Rect parent, QuadrantEnum quadrantEnum)
        {
            float width = parent.width * 0.5f;
            float height = parent.height * 0.5f;
            switch (quadrantEnum)
            {
                case QuadrantEnum.One:
                    return new Rect(parent.x + width, parent.y + height, width, height);
                case QuadrantEnum.Two:
                    return new Rect(parent.x, parent.y + height, width, height);
                case QuadrantEnum.Three:
                    return new Rect(parent.x, parent.y, width, height);
                case QuadrantEnum.Four:
                    return new Rect(parent.x + width, parent.y, width, height);
                default:
                    return parent;
            }
        }
    }
}

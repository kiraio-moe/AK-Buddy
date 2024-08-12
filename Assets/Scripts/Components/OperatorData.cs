using System;
using System.Collections.Generic;

namespace Arknights.Components
{
    [Serializable]
    public class OperatorData
    {
        public string AtlasPath;
        public string SkeletonPath;
        public string TexturePath;
        public List<string> VoicesPath = new();
    }
}

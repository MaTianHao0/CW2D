using Grasshopper.Kernel.Types;
using System.Collections.Generic;

namespace CW2D.Attributes
{
    internal class AttributeData
    {
        internal IGH_GeometricGoo Goo { get; }
        internal Dictionary<string, string> Attribute { get; set; }
        public AttributeData() { }
        public AttributeData(IGH_GeometricGoo goo, Dictionary<string, string> attribute)
        {
            Goo = goo;
            Attribute = attribute;
        }

        //修改全部属性
        public void ChangeAttribute(Dictionary<string, string> attribute)
        {
            Attribute = attribute;
        }

        //将属性key的值修改为value
        public void ChangeAttribute(string key, string value)
        {
            Attribute[key] = value;
        }
    }

    internal class GH_AttributeData : GH_Goo<AttributeData>
    {
        public GH_AttributeData() : base() { }

        public GH_AttributeData(AttributeData data) : base(data) { }

        public override bool IsValid => Value.Goo.IsValid;

        public override string TypeName => "属性数据";

        public override string TypeDescription => "包含几何体和其绑定的属性";

        public override IGH_Goo Duplicate() => new GH_AttributeData(Value);

        public override string ToString() => Value.ToString();
    }
}

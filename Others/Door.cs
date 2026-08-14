using Grasshopper.Kernel.Geometry;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CW2D.Others
{
    internal class TopViewDoor
    {
        private Point3d _pt;
        private Rectangle3d _rect;
        private Arc _arc;

        public Point3d Point { get { return _pt; } }

        public TopViewDoor() { }

    }
}

using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace CW2D_tukuang2
{
    public class CW2D_tukuang2Info : GH_AssemblyInfo
    {
        public override string Name => "CW2D_tukuang2";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("20257959-f151-4e98-ab9b-da4fdd42cc76");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
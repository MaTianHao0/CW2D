using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace CW2D_tuce
{
    public class CW2D_tuceInfo : GH_AssemblyInfo
    {
        public override string Name => "CW2D_tuce";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("36ad17b8-f584-4bf4-95c5-f5a70dd8a9bd");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
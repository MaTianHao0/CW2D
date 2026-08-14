using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace CW2D
{
    public class CW2DInfo : GH_AssemblyInfo
    {
        public override string Name => "CW2D";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("badb8472-b312-4ece-bf13-b45214379ced");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}
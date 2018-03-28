using System;
using Rhino.Compute;
using System.Collections.Generic;
using System.Threading.Tasks;
using Rhino.Geometry;
using System.Linq;

namespace MakeAMesh
{
    class Program
    {
        static void Main(string[] args)
        {
            ComputeServer.ApiToken = PUTTOKENHERE;

            ComputeIntersectionsTest();
            //CreateMeshesTest();

            Console.WriteLine("press any key to exit");
            Console.ReadKey();
        }

        static void ComputeIntersectionsTest()
        {
            var listOfDims = Enumerable.Range(1, 100).ToList();

            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < listOfDims.Count; i++)
            {
                var dim = listOfDims[i];
                var pt0 = new Point3d(-1, -1, -1);
                var pt1 = new Point3d(dim, dim, -1);
                var plane = new Plane(new Point3d(0, 0, -1), new Vector3d(0, 0, 1));
                var rectangle3D = new Rectangle3d(plane, pt0, pt1);
                var brep = Extrusion.Create(rectangle3D.ToNurbsCurve(), dim, true).ToBrep();
                brep.GetVolume();

                var dimPlus = dim + 1;

                var linePt0 = new Point3d(-2, -2, -2);
                var linePt1 = new Point3d(dimPlus, dimPlus, dimPlus);

                var success = Rhino.Compute.Intersect.IntersectionCompute.CurveBrep(new LineCurve(linePt0, linePt1), brep, 0.0001, 0, out double[] intersections);
            }
            stopwatch.Stop();
            Console.WriteLine($"Serial = {stopwatch.ElapsedMilliseconds} ms");

            
            stopwatch.Restart();
            var volumeTasks = new Task<double>[listOfDims.Count];
            var intersectionTasks = new Task<Tuple<bool, double[]>>[listOfDims.Count];
            for (int i = 0; i < listOfDims.Count; i++)
            {
                var dim = listOfDims[i];
                var pt0 = new Point3d(-1, -1, -1);
                var pt1 = new Point3d(dim, dim, -1);
                var plane = new Plane(new Point3d(0, 0, -1), new Vector3d(0, 0, 1));
                var rectangle3D = new Rectangle3d(plane, pt0, pt1);
                var brep = Extrusion.Create(rectangle3D.ToNurbsCurve(), dim, true).ToBrep();
                volumeTasks[i] = brep.GetVolumeAsync();

                var dimPlus = dim + 1;

                var linePt0 = new Point3d(-2, -2, -2);
                var linePt1 = new Point3d(dimPlus, dimPlus, dimPlus);

                intersectionTasks[i] = Rhino.Compute.Intersect.IntersectionCompute.CurveBrepAsync(new LineCurve(linePt0, linePt1), brep, 0.0001, 0);
            }
            Task.WaitAll(volumeTasks);
            Task.WaitAll(intersectionTasks);

            stopwatch.Stop();
            Console.WriteLine($"Tasks = {stopwatch.ElapsedMilliseconds} ms");
            

            stopwatch.Restart();
            var volumeBlocks = new ComputeBlock<double>[listOfDims.Count];
            var intersectionBlocks = new ComputeBlock<bool, double[]>[listOfDims.Count];
            for (int i = 0; i < listOfDims.Count; i++)
            {
                var dim = listOfDims[i];
                var pt0 = new Point3d(-1, -1, -1);
                var pt1 = new Point3d(dim, dim, -1);
                var plane = new Plane(new Point3d(0, 0, -1), new Vector3d(0, 0, 1));
                var rectangle3D = new Rectangle3d(plane, pt0, pt1);
                var brep = Extrusion.Create(rectangle3D.ToNurbsCurve(), dim, true).ToBrep();
                volumeBlocks[i] = brep.GetVolumeBulk();

                var dimPlus = dim + 1;

                var linePt0 = new Point3d(-2, -2, -2);
                var linePt1 = new Point3d(dimPlus, dimPlus, dimPlus);

                intersectionBlocks[i] = Rhino.Compute.Intersect.IntersectionCompute.CurveBrepBulk(new LineCurve(linePt0, linePt1), brep, 0.0001, 0);
            }
            ComputeServer.PostMultiple(volumeBlocks);
            ComputeServer.PostMultiple(intersectionBlocks);


            stopwatch.Stop();
            Console.WriteLine($"Multiple = {stopwatch.ElapsedMilliseconds} ms");
        }

        static void CreateMeshesTest()
        {
            const int totalCount = 10;
            var stopwatch = new System.Diagnostics.Stopwatch();

            // using standard calls
            stopwatch.Start();
            List<Mesh[]> meshes = new List<Mesh[]>();
            for (int i = 0; i < totalCount; i++)
            {
                var sphere = new Sphere(Point3d.Origin, 12 + i);
                var sphereAsBrep = sphere.ToBrep();
                meshes.Add(MeshCompute.CreateFromBrep(sphereAsBrep));
            }
            stopwatch.Stop();
            Console.WriteLine($"Serial = {stopwatch.ElapsedMilliseconds} ms");

            // using Tasks
            stopwatch.Restart();
            var tasks = new Task<Mesh[]>[totalCount];
            for (int i = 0; i < tasks.Length; i++)
            {
                var sphere = new Sphere(Point3d.Origin, 12 + i);
                var sphereAsBrep = sphere.ToBrep();
                tasks[i] = MeshCompute.CreateFromBrepAsync(sphereAsBrep);
            }
            Task.WaitAll(tasks);
            stopwatch.Stop();
            Console.WriteLine($"Tasks = {stopwatch.ElapsedMilliseconds} ms");


            // using blocks
            stopwatch.Restart();
            var blocks = new ComputeBlock<Mesh[]>[totalCount];
            for (int i = 0; i < totalCount; i++)
            {
                var sphere = new Sphere(Point3d.Origin, 12 + i);
                var sphereAsBrep = sphere.ToBrep();
                blocks[i] = MeshCompute.CreateFromBrepBulk(sphereAsBrep);
            }
            ComputeServer.PostMultiple(blocks.ToArray());
            stopwatch.Stop();
            Console.WriteLine($"Multiple = {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}

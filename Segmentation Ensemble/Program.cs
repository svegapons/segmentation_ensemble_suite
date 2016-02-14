using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using Segmentation_Ensemble.SE;

namespace Segmentation_Ensemble
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("***Segmentation Ensemble via Kernels***");
            Console.WriteLine();
            Console.WriteLine("Please, introduce the path of the folder that contains the data to be processed.");
            Console.WriteLine("This folder should contain an image and some segmentations of this image.");
           // try
            {
                //string str = Console.ReadLine();
                string str = @"C:\Users\Mijis\Desktop\Segmentation Ensemble\Segmentation Ensemble\bin\Debug\imgs";
                Problem p = new Problem(str);
                SEK sek = new SEK(p);
                Bitmap bmp = sek.BuildStructuring();
                bmp.Save(str + "\\result.jpg");
            }
            //catch (Exception ex)    
            //{
            //    Console.WriteLine(ex.Message);
            //}
            //Console.ReadLine();
        }
    }
}

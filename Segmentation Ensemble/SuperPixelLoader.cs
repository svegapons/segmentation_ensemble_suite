using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace Segmentation_Ensemble
{
    public class SuperPixelLoader:ILoader
    {
        FileStream filestream;
        public SuperPixelLoader()
        {
        }
        public SuperPixelLoader(string filepath)
        {
            this.SourceFilePath = filepath;
        }

        #region ILoader Members

        public string SourceFilePath
        {
            get;
            set;
        }

        public void SetSource(FileStream file)
        {
            this.filestream = file;
        }

        public void SetSource(string filepath)
        {
            this.SourceFilePath = filepath;
        }

        public void ResetSource()
        {
            throw new NotImplementedException();
        }

       

        public bool TryLoad()
        {
            throw new NotImplementedException();
        }

        public bool TryLoad(string filepath)
        {
            string tempfilepath = SourceFilePath;

            SourceFilePath = filepath;
            bool result = TryLoad();
            SourceFilePath = tempfilepath;

            return result;
        }

       

        public Set Load(string filepath)
        {
            string tempfilepath = SourceFilePath;

            SourceFilePath = filepath;
            Set result = Load();
            SourceFilePath = tempfilepath;

            return result;
        }

        public Set Load()
        {
            Set set = null;
            Bitmap bmp = (Bitmap)Bitmap.FromFile(SourceFilePath);
            //Ya tengo los atributos
            Attributes attributes = new Attributes(new List<Attribute>(new Attribute[] { new Attribute("PixelsCount", null), new Attribute("R_Ave", null), new Attribute("G_Ave", null), new Attribute("B_Ave", null), new Attribute("R_error", null), new Attribute("G_error", null), new Attribute("B_error", null), new Attribute("X_GravCenter", null), new Attribute("Y_GravCenter", null), new Attribute("Radius", null), new Attribute("Perimeter", null) }));
            //Ya tengo el nombre
            string relationName = Path.GetFileNameWithoutExtension(SourceFilePath);
            //Faltan los elementos...para esto tengo q cargar el archivo q contiene la informacion de los superpixels.

            string directoryName = Path.GetDirectoryName(SourceFilePath);
            string superPixelPath = directoryName + "//SP_supPix" + relationName + ".mat.txt";
            int[,] sp = LoadSupPixMatrix(superPixelPath);
            Dictionary<int, List<Pixel>> spDic = GetSPDictionary(bmp, sp);

            SuperPixelGraph graph = new SuperPixelGraph(sp, spDic.Count);

            //Ya tengo los elementos
            List<Element> elements = GetElements(spDic, graph);

            set = new Set(relationName, elements, attributes);
            set.Image = bmp;
            set.FolderName = directoryName;
            set.SPGraph = graph;
            set.SuperPixelMatrix = sp;
            set.Segmentations = LoadSegmentations(SourceFilePath, sp, elements.Count);
            set.KValue = this.kvalue;
            set.GroundTruths = LoadGTs(SourceFilePath);


            //PRUEBAAAA...BORRAR
            //int[] l0 = new int[set.Segmentations[0].Labels.Length];
            //int[] l1 = new int[set.Segmentations[0].Labels.Length];
            //for (int i = 0; i < l0.Length; i++)
            //    l0[i] = i;

            //Segmentation seg0 = new Segmentation(l0);
            //Segmentation seg1 = new Segmentation(l1);
            //set.Segmentations.Add(seg0);
            //set.Segmentations.Add(seg1);
            ///

            return set;
        }


        #endregion


        //Carga la matrix de superpixels desde un fichero
        private int[,] LoadSupPixMatrix(string path)
        {
            StreamReader sr = new StreamReader(path);
            List<List<int>> mat = new List<List<int>>();
            int[,] matrix;
            string[] read;
            while (!sr.EndOfStream)
            {
                read = sr.ReadLine().Split('\t', ' ', (char)1);
                mat.Add(new List<int>());
                for (int i = 0; i < read.Length - 1; i++)
                {
                    string aux = read[i].Trim();
                    double res;
                    if (!double.TryParse(aux, out res))
                    {
                        for (int j = 0; j < aux.Length; j++)
                        {
                            char ch = aux[j];
                        }
                        aux = aux.Replace('*', '.');
                        if (!double.TryParse(aux, out res))
                            aux = "1";                   
                    }
                    int parse = (int)double.Parse(aux);
                    if (parse < 1)
                        parse = 1;
                    mat[mat.Count - 1].Add(parse);

                }
            }

            matrix = new int[mat.Count, mat[0].Count];
            for (int i = 0; i < mat.Count; i++)
            {
                for (int j = 0; j < mat[0].Count; j++)
                {
                    if (j >= mat[i].Count) //Este if es un parchezón pq los datos de lucas tenian algun problemitaen el TBES_UCM
                        matrix[i, j] = 0;
                    else
                    //Aqui resto uno para llevar la matriz de superpixels al rango [0, n-1] mas comodo para trabajar aqui en C# q el original [1, n].
                    matrix[i, j] = mat[i][j] -1;
                }
            }
            sr.Close();
            return matrix;
        }

        //Devuelve un hashtable donde por cada superpixel se tiene la lista de pixeles cada uno con la info de posicion en la imagen y valores de color
        private Dictionary<int, List<Pixel>> GetSPDictionary(Bitmap bmp, int[,] sp)
        {
            Dictionary<int, List<Pixel>> result = new Dictionary<int, List<Pixel>>();
            int current = 0;
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
               ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* imgPtr = (byte*)(data.Scan0);
                for (int i = 0; i < data.Height; i++)
                {
                    for (int j = 0; j < data.Width; j++)
                    {
                        current = sp[i, j];
                        if (!result.ContainsKey(current))
                            result.Add(current, new List<Pixel>());
                        result[current].Add(new Pixel(i, j, (int)imgPtr[2], (int)imgPtr[1], (int)imgPtr[0]));
                    }
                    imgPtr += data.Stride - data.Width * 3;
                }
            }
            bmp.UnlockBits(data);
            return result;
        }

        //Devuelve la lista de superpixels como elementos a partir del diccionario de los superpixels
        private List<Element> GetElements(Dictionary<int, List<Pixel>> spDic, SuperPixelGraph graph)
        {
            //cp--cantidad de pixels, r--R, g--G, b--B (RGB values), cgx--coordenada x del centro de gravedad, cgy--coordenada y del centro de gravedad, ra--radio, definido como la distancia del pixel mas alejado al centro de gravedad.
            List<Element> elements = new List<Element>();

            foreach (int spID in spDic.Keys)    
            {
                List<object> realvalues = new List<object>();
                int cp = 0, r = 0, g = 0, b = 0, cgx = 0, cgy = 0, ra = 0;

                for (int j = 0; j < spDic[spID].Count; j++)
                {
                    cp++;
                    r += spDic[spID][j].R;
                    g += spDic[spID][j].G;
                    b += spDic[spID][j].B;
                    cgx += spDic[spID][j].j;
                    cgy += spDic[spID][j].i;
                }
                r /= spDic[spID].Count;
                g /= spDic[spID].Count;
                b /= spDic[spID].Count;
                cgx /= spDic[spID].Count;
                cgy /= spDic[spID].Count;

                int aux=0, auxI=0, auxJ = 0;
                double r_error = 0, g_error = 0, b_error = 0;
                int perim =0;
                for (int j = 0; j < spDic[spID].Count; j++)
                {
                    auxI = spDic[spID][j].i;
                    auxJ = spDic[spID][j].j;
                    aux = (int)Math.Sqrt(((auxI - cgy) * (auxI - cgy)) + ((auxJ - cgx) * (auxJ - cgx)));
                    if (aux > ra)
                        ra = aux;

                    r_error += Math.Abs(spDic[spID][j].R - r);
                    g_error += Math.Abs(spDic[spID][j].G - g);
                    b_error += Math.Abs(spDic[spID][j].B - b);
                }
                r_error /= spDic[spID].Count;
                g_error /= spDic[spID].Count;
                b_error /= spDic[spID].Count;

                perim = graph.PerimList[spID];

                realvalues.Add(cp); realvalues.Add(r); realvalues.Add(g); realvalues.Add(b); realvalues.Add(r_error); realvalues.Add(g_error); realvalues.Add(b_error); realvalues.Add(cgx); realvalues.Add(cgy); realvalues.Add(ra); realvalues.Add(perim);
                Element e = new Element(realvalues);
                e.Name = "SP-" + spID;
                e.Index = spID;
                elements.Add(e);
            }
            return elements;
        }

        int kvalue = 0;
        //Carga las segmentaciones
        //El path es la direccion de la imagen...pa cargar las segmentaciones se van a buscar los txt q empiecen con Seg en el nombre y q esten en la misma carpeta.
        private List<Segmentation> LoadSegmentations(string path, int[,] sp, int spCount)
        {
            int max = 0;
            List<int> kvalues = new List<int>();
            string directory = Path.GetDirectoryName(path);
            List<Segmentation> result = new List<Segmentation>(10);
            foreach (string str in Directory.GetFiles(directory))
            {
                if (Path.GetFileNameWithoutExtension(str).Substring(0, 3) == "Seg")
                {
                    max = 0;
                    int[,] seg = LoadSupPixMatrix(str);
                    int[] labels = new int[spCount];

                    //Este doble ciclo es super ineficiente...esto se puede resolver dejando la pos de un pixel como representante del superpixel y buscar solo esos valores en el archivo de la segmentacion.
                    //HACER EL CAMBIO PARA LA VERSION EFICIENTE!!!
                    for (int i = 0; i < seg.GetLength(0); i++)
                    {
                        for (int j = 0; j < seg.GetLength(1); j++)
                        {
                            labels[sp[i, j]] = seg[i, j];
                            if (max < seg[i, j])
                                max = seg[i, j];
                        }
                    }
                    kvalues.Add(max);
                    result.Add(new Segmentation(labels));
                }
            }
            kvalues.Sort();
            if (kvalues.Count % 2 == 1)
                kvalue = kvalues[kvalues.Count / 2];
            else
                kvalue = (kvalues[(kvalues.Count - 1) / 2] + kvalues[kvalues.Count / 2]) / 2;
            return result;
        }

         //Carga los ground-truth
        //El path es la direccion de la imagen...pa cargar los ground-truth se van a buscar los .seg q esten en la misma carpeta.
        private List<Segmentation> LoadGTs(string path)
        {
            string directory = Path.GetDirectoryName(path);
            List<Segmentation> result = new List<Segmentation>();
            foreach (string str in Directory.GetFiles(directory))
            {
                if (Path.GetExtension(str) == "seg" || Path.GetExtension(str) == ".seg")
                {
                    result.Add(LoadSEG(str));
                }
            }
            return result;
        }

        //Dada la direccion exacta de donde esta el archivo .seg carga el ground-truth como un objeto segmentacion
        //En este caso la segmentacion es del tamanno del numero de pixels y no del numro de superpixels.
        private Segmentation LoadSEG(string path)
        {
            StreamReader sr = new StreamReader(path);
            sr.ReadLine(); //tipo de archivo...asumo "ascii cr"
            sr.ReadLine(); //fecha...no me importa
            string name = sr.ReadLine().Split(' ')[1] + "_" + sr.ReadLine().Split(' ')[1]; //Nombre es "#image_#user"
            int width = int.Parse(sr.ReadLine().Split(' ')[1]); //cojo el width
            int height = int.Parse(sr.ReadLine().Split(' ')[1]); //cojo el height
            int segment = int.Parse(sr.ReadLine().Split(' ')[1]); //cojo la cantidad de segmentaciones
            sr.ReadLine();   // En gray-scale?...voy a asumir q no
            sr.ReadLine();  //invertido los pixeles?...asumo que no
            sr.ReadLine();   //ni se....asumo q no
            sr.ReadLine();  //leer la palabra "data".

            int[,] matrix = new int[height, width];
            int s, r, c1, c2;
            while (!sr.EndOfStream)
            {
                string[] line = sr.ReadLine().Split(' ');
                s = int.Parse(line[0]);
                r = int.Parse(line[1]);
                c1 = int.Parse(line[2]);
                c2 = int.Parse(line[3]);
                for (int i = c1; i <= c2; i++)
                    matrix[r, i] = s;
            }
            sr.Close();
            int[] labels = new int[matrix.Length];
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    labels[i * matrix.GetLength(1) + j] = matrix[i, j];
                }
            }

            return new Segmentation(labels);
        }
    }


    class Pixel
    {
        internal int i, j, R, G, B;
        public Pixel(int i, int j, int R, int G, int B)
        {
            this.i = i;
            this.j = j;
            this.R = R;
            this.G = G;
            this.B = B;
        }

    }

}

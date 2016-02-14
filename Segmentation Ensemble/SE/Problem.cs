using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;

namespace Segmentation_Ensemble.SE
{
    public class Problem
    {
        
        public SuperPixelGraph SPGraph { get; set; }
        public List<Segmentation> Segmentations { get; set; }
        List<int> kvalues = new List<int>();
        public Bitmap Image { get; set; }
        public string ImageName { get; set; }
        public int[,] SuperPixelMatrix { get; set; }
        public Bitmap FinalResult { get; set; }
        public string FolderName { get; set; }
        public double DistToConsensus { get; set; }
        public int KValue { get; set; }
        //Estas segmentaciones van a ser mas grandes..(del tamanno del numero de pixels no del numero de superpixels)
        public List<Segmentation> GroundTruths { get; set; }

        //Voy a guardar en el mismo objeto problem las evaluaciones del resultado contra el mejor ground-truth y el promedio contra todos.
        public double Ave_RandEvaluation { get; set; }
        public double Ave_VIEvaluation { get; set; }
        public double Ave_NMIEvaluation { get; set; }

        public double Best_RandEvaluation { get; set; }
        public double Best_VIEvaluation { get; set; }
        public double Best_NMIEvaluation { get; set; }

        List<Bitmap> segsImg = new List<Bitmap>();

        public Problem(string path)
        {
            string[] files = Directory.GetFiles(path);
            string nameSeg = "";
            foreach (string str in files)
            {
                nameSeg = Path.GetFileName(str);
                if (nameSeg.StartsWith("Seg_") || nameSeg.StartsWith("seg_") || nameSeg.StartsWith("SEG_"))
                    segsImg.Add((Bitmap)Bitmap.FromFile(str));
                else
                    Image = (Bitmap)Bitmap.FromFile(str);
            }

            this.SuperPixelMatrix = new int[Image.Height, Image.Width];
            this.Segmentations = new List<Segmentation>();

            CreateSuperPixelMatrix();
            Dictionary<int, List<Pixel>> spDic = GetSPDictionary(Image, SuperPixelMatrix);

            SPGraph = new SuperPixelGraph(SuperPixelMatrix, spDic.Count);
            //poner aqui el resto de las cosas q estan en el load.
        }


        //public void Load()
        //{
        //    //Ya tengo los atributos
        //   // Attributes attributes = new Attributes(new List<Attribute>(new Attribute[] { new Attribute("PixelsCount", null), new Attribute("R_Ave", null), new Attribute("G_Ave", null), new Attribute("B_Ave", null), new Attribute("R_error", null), new Attribute("G_error", null), new Attribute("B_error", null), new Attribute("X_GravCenter", null), new Attribute("Y_GravCenter", null), new Attribute("Radius", null), new Attribute("Perimeter", null) }));
        //    //Ya tengo el nombre
        //   // string relationName = Path.GetFileNameWithoutExtension(SourceFilePath);
        //    //Faltan los elementos...para esto tengo q cargar el archivo q contiene la informacion de los superpixels.

        //   // string directoryName = Path.GetDirectoryName(SourceFilePath);
        //   // string superPixelPath = directoryName + "//SP_supPix" + relationName + ".mat.txt";
        //  //  int[,] sp = LoadSupPixMatrix(superPixelPath);

           

        //    //Ya tengo los elementos
        //    //List<Element> elements = GetElements(spDic, graph);

        //    //set = new Set(relationName, elements, attributes);
        //    //set.Image = bmp;
        //    //set.FolderName = directoryName;
        //    //set.SPGraph = graph;
        //    //set.SuperPixelMatrix = sp;

        //    LoadSegmentations(spDic.Count);
        //    set.KValue = this.kvalue;
        //    set.GroundTruths = LoadGTs(SourceFilePath);
        //}

        //Carga las segmentaciones
        //El path es la direccion de la imagen...pa cargar las segmentaciones se van a buscar los txt q empiecen con Seg en el nombre y q esten en la misma carpeta.
        //private void LoadSegmentations(int spCount)
        //{
        //    int max = 0;
        //    List<int> kvalues = new List<int>();
        //    //string directory = Path.GetDirectoryName(path);
        //    //List<Segmentation> result = new List<Segmentation>(10);

        //    foreach (Bitmap bmp in segsImg)
        //    {
                
        //    }



        //    foreach (string str in Directory.GetFiles(directory))
        //    {
        //        if (Path.GetFileNameWithoutExtension(str).Substring(0, 3) == "Seg")
        //        {
        //            max = 0;
        //            int[,] seg = LoadSupPixMatrix(str);
        //            int[] labels = new int[spCount];

        //            //Este doble ciclo es super ineficiente...esto se puede resolver dejando la pos de un pixel como representante del superpixel y buscar solo esos valores en el archivo de la segmentacion.
        //            //HACER EL CAMBIO PARA LA VERSION EFICIENTE!!!
        //            for (int i = 0; i < seg.GetLength(0); i++)
        //            {
        //                for (int j = 0; j < seg.GetLength(1); j++)
        //                {
        //                    labels[sp[i, j]] = seg[i, j];
        //                    if (max < seg[i, j])
        //                        max = seg[i, j];
        //                }
        //            }
        //            kvalues.Add(max);
        //            result.Add(new Segmentation(labels));
        //        }
        //    }
        //    kvalues.Sort();
        //    if (kvalues.Count % 2 == 1)
        //        kvalue = kvalues[kvalues.Count / 2];
        //    else
        //        kvalue = (kvalues[(kvalues.Count - 1) / 2] + kvalues[kvalues.Count / 2]) / 2;
        //    return result;
        //}


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

        //Calculo la matriz de superpixeles y dejo el resultado en la variable SuperPixelMatrix. Ademas ya aprovecho y cargo las segmentacions y las represento a partir de los superpixeles.
        private void CreateSuperPixelMatrix()
        {
            //estas van a ser las matrices de segmentaciones q me daba antes Lucas. Ahora las creo dinamicamente y de ahi creo los objetos segmentacion basado en superpixeles.
            List<int[,]> segMat = new List<int[,]>();
            for (int i = 0; i < segsImg.Count; i++)
                segMat.Add(new int[Image.Height, Image.Width]);

            Dictionary<string, int> aux = new Dictionary<string, int>();

            BitmapData[] bitdata = new BitmapData[segsImg.Count];
            string[,] idClusterMeet = new string[Image.Height, Image.Width];
            string str="";
            int c = 0;
            byte r=0, g=0, b=0;

            for (int i = 0; i < bitdata.Length; i++)
            {
                aux = new Dictionary<string, int>();
                c = 0;
                bitdata[i] = segsImg[i].LockBits(new Rectangle(0, 0, segsImg[i].Width, segsImg[i].Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

                unsafe
                {
                    byte* imgPtr = (byte*)(bitdata[i].Scan0);
                    for (int k = 0; k < bitdata[i].Height; k++)
                    {
                        for (int j = 0; j < bitdata[i].Width; j++)
                        {
                            r = imgPtr[0];
                            g = imgPtr[1];
                            b = imgPtr[2];

                            imgPtr += 3;

                            str = r.ToString() + "*" + g.ToString() + "*" + b.ToString();
                            //str = ((int)r + (int)g + (int)b).ToString();

                            idClusterMeet[k, j] += str + "-";
                            if (!aux.ContainsKey(str))
                            {
                                aux.Add(str, c);
                                segMat[i][k, j] = c;
                                c++;
                            }
                            else
                                segMat[i][k, j] = aux[str];
                        }
                        imgPtr += bitdata[i].Stride - bitdata[i].Width * 3;
                    }
                    segsImg[i].UnlockBits(bitdata[i]);
                }
            }

            int cont = 0;
            Dictionary<string, int> visited = new Dictionary<string, int>();
            for (int i = 0; i < idClusterMeet.GetLength(0); i++)
            {
                for (int j = 0; j < idClusterMeet.GetLength(1); j++)
                {
                    if (!visited.ContainsKey(idClusterMeet[i, j]))
                    {
                        visited.Add(idClusterMeet[i, j], cont);
                        SuperPixelMatrix[i, j] = cont;
                        cont++;
                    }
                    else
                        SuperPixelMatrix[i, j] = visited[idClusterMeet[i, j]];
                }
            }

            //La cantidad de superpixeles es cont+1.
            int[] labels = new int[cont+1];
            int max = 0;
            kvalues = new List<int>();

            for (int k = 0; k < segsImg.Count; k++)
            {
                for (int i = 0; i < SuperPixelMatrix.GetLength(0); i++)
                {
                    for (int j = 0; j < SuperPixelMatrix.GetLength(1); j++)
                    {
                        labels[SuperPixelMatrix[i, j]] = segMat[k][i, j];
                        if (max < segMat[k][i, j])
                            max = segMat[k][i, j];
                    }
                }
            kvalues.Add(max);
            Segmentations.Add(new Segmentation(labels)); ///voy por aqui.
            }
            
          
        }
    }


    public class SuperPixelGraph
    {
        //Aqui voy a guardar los superpixels solo por su numero (index). Sin el "SP-" delante.
        //Voy a trabajar en la 4-vecindad pa definir pixeles vecinos.
        public SuperPixelGraph(int[,] spMatrix, int spCount)
        {
            adjList = new List<int>[spCount];
            adjMatrix = new int[spCount, spCount];
            perimList = new int[spCount];

            //Llenar la matrix de adjacencia...en el sentido de las x
            for (int i = 0; i < spMatrix.GetLength(0); i++)
                for (int j = 0; j < spMatrix.GetLength(1) - 1; j++)
                {
                    if (i == 0)
                        perimList[spMatrix[i, j]]++;
                    if (i == spMatrix.GetLength(0) - 1)
                        perimList[spMatrix[i, j]]++;
                    if (j == 0)
                        perimList[spMatrix[i, j]]++;
                    if (j == spMatrix.GetLength(1) - 2)
                        perimList[spMatrix[i, j + 1]]++;

                    if (spMatrix[i, j] != spMatrix[i, j + 1])
                    {
                        adjMatrix[spMatrix[i, j], spMatrix[i, j + 1]]++;
                        adjMatrix[spMatrix[i, j + 1], spMatrix[i, j]]++;
                        perimList[spMatrix[i, j]]++;
                        perimList[spMatrix[i, j + 1]]++;
                    }
                }

            //Llenar la matrix de adjacencia...en el sentido de las y
            for (int j = 0; j < spMatrix.GetLength(1); j++)
                for (int i = 0; i < spMatrix.GetLength(0) - 1; i++)
                    if (spMatrix[i, j] != spMatrix[i + 1, j])
                    {
                        adjMatrix[spMatrix[i, j], spMatrix[i + 1, j]]++;
                        adjMatrix[spMatrix[i + 1, j], spMatrix[i, j]]++;
                        perimList[spMatrix[i, j]]++;
                        perimList[spMatrix[i + 1, j]]++;
                    }

            //Lista de adyacencia...voy a poner los superpixeles asociados a cada superpixel de acuerdo a la cantidad de pixeles q comparten...primero el q mas hasta el q menos.

            for (int i = 0; i < adjMatrix.GetLength(0); i++)
            {
                SortedList<double, int> edges = new SortedList<double, int>();
                for (int j = 0; j < adjMatrix.GetLength(1); j++)
                {
                    double curr = adjMatrix[i, j];
                    if (curr != 0)
                    {
                        while (edges.ContainsKey(curr))
                            curr -= 0.01;
                        edges.Add(curr, j);
                    }
                }
                adjList[i] = new List<int>();

                foreach (double val in edges.Keys)
                {
                    adjList[i].Add(edges[val]);
                }
                adjList[i].Reverse();
            }
        }

        int[] perimList;
        public int[] PerimList
        {
            get { return perimList; }
        }

        List<int>[] adjList;
        public List<int>[] AdjList
        {
            get { return adjList; }
        }

        int[,] adjMatrix;
        public int[,] AdjMatrix
        {
            get { return adjMatrix; }
        }
    }


    public class Segmentation
    {
        public int[] Labels { get; set; }
        public int ClusterCount { get; set; }
        public Dictionary<int, List<int>> Clusters { get; set; }

        //esta variable hace falta para cuando voy a crear un vecino poniendo un objeto en un cluster solo hace falta conocer bien rapido un numero de etiqueta de cluster q no este asignado a nadie. 
        //Con ClusterCount no se resuelve pq en el proceso de construccion de vecinos se puede perder el hecho de q todos los clusters estan enumerados con etiquetas desde 0 hasta ClusterCount-1.
        private int maxLabel = 0;

        public Segmentation(int[] labels)
        {
            Labels = labels;
            Clusters = new Dictionary<int, List<int>>();
            for (int i = 0; i < labels.Length; i++)
            {
                if (!Clusters.ContainsKey(Labels[i]))
                    Clusters.Add(Labels[i], new List<int>());
                Clusters[Labels[i]].Add(i);
                if (Labels[i] > maxLabel)
                    maxLabel = Labels[i];
            }
            ClusterCount = Clusters.Count;
        }

        public Segmentation GetNeighbor(SuperPixelGraph spg)
        {
            int[] newLabels = new int[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
                newLabels[i] = Labels[i];
            Random r = new Random();

            //posicion del objeto q voy a cambiar de region
            int pos = r.Next(0, Labels.Length-1);  //le meti el -1..analizar

            //numero de la nueva region a seleccionar entre las posibles segun el grafo de la imagen
            int reg = r.Next(0, spg.AdjList[pos].Count - 1);  //le meti el -1..analizar

            //este es el superpixel con el q me voy a pegar en el mismo cluster.
            int sp = spg.AdjList[pos][reg];

            //Esta es la etiqueta del cluster al q voy a ser asignado.
            int newL = Labels[sp];

            //Si la nueva posicion es la misma, cojo esta posibilidad para poner el elemento en un nuevo cluster donde esta el solo.
            if (newLabels[pos] == newL)
                newLabels[pos] = this.maxLabel + 1;
            else
                newLabels[pos] = newL;
            Segmentation result = new Segmentation(newLabels);
            return result;
        }

        //Aqui estoy asumiendo q ya la segmentacion inicial tiene k clusters, con k el valor q quiero q tengan todas.
        public Segmentation GetNeighborKFIJO(SuperPixelGraph spg)
        {
            int[] newLabels = new int[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
                newLabels[i] = Labels[i];
            Random r = new Random();

            //posicion del objeto q voy a cambiar de region
            int pos = 0;
            do
            {
                pos = r.Next(0, Labels.Length);
            }
            while (this.Clusters[Labels[pos]].Count <= 1);

            //numero de la nueva region a seleccionar entre las posibles segun el grafo de la imagen
            int reg = r.Next(0, spg.AdjList[pos].Count);

            //este es el superpixel con el q me voy a pegar en el mismo cluster.
            int sp = spg.AdjList[pos][reg];

            //Esta es la etiqueta del cluster al q voy a ser asignado.
            int newL = Labels[sp];


            //Tiene el problema de q si es la misma etiqueta...estoy generando la misma segmentacion de nuevo!!
            newLabels[pos] = newL;
            Segmentation result = new Segmentation(newLabels);
            return result;
        }


        //Devuelve una nueva segmentacion que se forma al separar "clusCount" clusters (en dos nuevos clusters) en la segmentacion actual.
        public Segmentation Split(int clusCount, SuperPixelGraph spg)
        {
            int[] newLabels = new int[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
                newLabels[i] = Labels[i];
            Random r = new Random();
            int last = this.maxLabel;

            for (int i = 0; i < clusCount; i++)
            {
                int pos = r.Next(0, Labels.Length);
                newLabels[pos] = last + 1;
                last++;

            }
            Segmentation result = new Segmentation(newLabels);
            return result;
        }

        //Devuelve una nueva segmentacion que se forma al unir "clusCount" pares de clusters en la segmentacion actual.
        public Segmentation Join(int clusCount, SuperPixelGraph spg)
        {
            int[] newLabels = new int[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
                newLabels[i] = Labels[i];
            Random r = new Random();

            for (int i = 0; i < clusCount; i++)
            {
                int pos = r.Next(0, Labels.Length);
                int reg = r.Next(0, spg.AdjList[pos].Count);
                int sp = spg.AdjList[pos][reg];
                int newL = newLabels[sp];

                int value = newLabels[pos];
                for (int j = 0; j < newLabels.Length; j++)
                {
                    if (newLabels[j] == value)
                        newLabels[j] = newL;
                }
            }
            Segmentation result = new Segmentation(newLabels);
            return result;
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


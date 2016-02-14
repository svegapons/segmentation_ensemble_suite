using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;

namespace Segmentation_Ensemble.SE
{
    public class SEK
    {
        public SEK(Problem prob)
        {
            this.prob = prob;
        }

        //List<SegmentationIndex> indexes = new List<SegmentationIndex>();
        Problem prob;
        RandIndexSegSim rand = new RandIndexSegSim();
        //KernelSubsetSignifSegSim rand = new KernelSubsetSignifSegSim();

        public Bitmap BuildStructuring()
        {

            //FillSegmentationIndexList();
            //double[] weights = ComputeWeights();
            double[] weights = new double[prob.Segmentations.Count];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = 1.0;

            double lTerm = Compute3thTerm(weights);
            double[] normWeights = new double[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                //Esta es la normalizacion propuesta en el paper. Esto implica q el 3er termino ahora con estos pesos es igual a 1.
                normWeights[i] = weights[i] / Math.Sqrt(lTerm);
            }

            Segmentation s0 = GetS0(normWeights);
           // Segmentation best = s0;
            Segmentation best = SimulatedAnnealing(normWeights, s0, 500);
            Segmentation segPixels = GetSegmentationResult(best);

            //Esto lo tengo desactivado pq no estoy evaluando segmentaciones..
            //this.EvaluateMatrixSegmentation(segPixels);
            
            this.prob.FinalResult = GetFinalSegImage(segPixels);
            
            //PrintFinalResults();

            return prob.FinalResult;
        }

        //Este metodo lo q hace es llenar la lista de indices con los indices q se van a usar en este caso  (AHORA TENGO DESHABILITADO TODO LO Q TIENE Q VER CON LOS INDICES).
        //private void FillSegmentationIndexList()
        //{
        //    //indexes = new List<SegmentationIndex>();
        //    //indexes.Add(new CompactnessIndex());
        //    //indexes.Add(new CircularityIndex());
        //    //indexes.Add(new ConnectivityIndex());
        //}

        //private double[] ComputeWeights()
        //{
        //    double[] weights = new double[this.prob.Segmentations.Count];
        //    foreach (SegmentationIndex sidx in indexes)
        //        sidx.EvaluateSegmentations(this.prob);

        //    double min = double.MaxValue; double max = 0;
        //    for (int i = 0; i < this.prob.Segmentations.Count; i++)
        //    {
        //        foreach (SegmentationIndex sidx in indexes)
        //            weights[i] += sidx.Weights[i];
        //        if (weights[i] < min)
        //            min = weights[i];
        //        if (weights[i] > max)
        //            max = weights[i];
        //    }

        //    for (int i = 0; i < weights.Length; i++)
        //    {
        //        //weights[i] = weights[i] / max;
        //        //Otra variante
        //        weights[i] = (weights[i] - min) / (max - min);
        //    }

        //    return weights;
        //}

        private double Compute3thTerm(double[] weights)
        {
            double result = 0;

            for (int i = 0; i < prob.Segmentations.Count; i++)
            {
                for (int j = 0; j < prob.Segmentations.Count; j++)
                {
                    result += weights[i] * weights[j] * rand.Process(prob.Segmentations[i], prob.Segmentations[j]);
                }
            }
            return result;
        }

        private double DistConsensus(double[] normWeights, Segmentation seg)
        {
            //Sumo 1 del primer termino y 1 del tercero..solo falta restar el 2do termino de la ecuacion (10) en el paper PR.
            double result = 2;
            for (int i = 0; i < prob.Segmentations.Count; i++)
            {
                result -= 2 * normWeights[i] * rand.Process(seg, prob.Segmentations[i]);
            }
            return result;
        }

        private Segmentation GetS0(double[] normWeights)
        {
            Segmentation closest = null;
            double min = double.MaxValue, current = 0;
            for (int i = 0; i < prob.Segmentations.Count; i++)
            {
                current = DistConsensus(normWeights, prob.Segmentations[i]);
                if (current < min)
                {
                    min = current;
                    closest = prob.Segmentations[i];
                }
            }
            return closest;
        }

        private Segmentation SimulatedAnnealing(double[] normWeights, Segmentation s0, int maxIter)
        {
            Segmentation current = s0;
            Segmentation next = s0;
            Segmentation best = s0;
            double e0 = DistConsensus(normWeights, s0);
            double eCurrent = e0;
            double eNext = e0;
            double eBest = e0;
            int r = 0;
            double temp = 0.0001428;
            Random random = new Random();


            //int[] l0 = new int[s0.Labels.Length];
            //int[] l1 = new int[s0.Labels.Length];
            //for (int i = 0; i < l0.Length; i++)
            //    l0[i] = i;

            //Segmentation seg0 = new Segmentation(l0);
            //Segmentation seg1 = new Segmentation(l1);
            //double au0 = DistConsensus(normWeights, seg0);
            //double au1 = DistConsensus(normWeights, seg1);


            while (r < maxIter)
            {
                next = current.GetNeighbor(prob.SPGraph);
                eNext = DistConsensus(normWeights, next);
                if (eNext < eBest)
                {
                    best = next;
                    eBest = eNext;
                }
                if (Math.Exp(-1 * (eNext - eBest) / temp) > random.NextDouble())
                {
                    current = next;
                    eCurrent = eNext;
                }

                if (r > (maxIter * 4) / 5 && r < (maxIter * 9) / 10)
                    temp = temp * 1.002;
                r++;

            }

            this.prob.DistToConsensus = eBest;
            return best;
        }

        //Esto convierte el objeto segmentacion en la real segmantacion haciendo el proceso contrario al calculo de los superpixels.
        //El nuevo objeto segmentacion es mucho mas grande....la cardinalidad es la cantidad de pixels y no la cantidad de superpixels.
        private Segmentation GetSegmentationResult(Segmentation best)
        {
            int[] result = new int[this.prob.SuperPixelMatrix.Length];
            for (int i = 0; i < this.prob.SuperPixelMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < this.prob.SuperPixelMatrix.GetLength(1); j++)
                {
                    int sp = this.prob.SuperPixelMatrix[i, j];
                    result[i * this.prob.SuperPixelMatrix.GetLength(1) + j] = best.Labels[sp];
                }
            }
            return new Segmentation(result);
        }

        //Evaluo la segmentacion con las medidas RandIndex, VI and NMI y dejo los resultados en el prob. Evaluo contra el mejor ground-truth y el promedio contra todos.
        private void EvaluateMatrixSegmentation(Segmentation segm)
        {
            RandIndexSegSim rand = new RandIndexSegSim();
            NMISegSim nmi = new NMISegSim();
            VISegSim vi = new VISegSim();

            //Calcular el rand
            double min = double.MaxValue;
            double ave = 0;
            double aux = 0;
            for (int i = 0; i < this.prob.GroundTruths.Count; i++)
            {
                aux = 1 - rand.Process(segm, this.prob.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.prob.GroundTruths.Count;
            this.prob.Best_RandEvaluation = min;
            this.prob.Ave_RandEvaluation = ave;

            //Calcular NMI
            min = double.MaxValue;
            ave = 0;
            aux = 0;
            for (int i = 0; i < this.prob.GroundTruths.Count; i++)
            {
                aux = 1 - nmi.Process(segm, this.prob.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.prob.GroundTruths.Count;
            this.prob.Best_NMIEvaluation = min;
            this.prob.Ave_NMIEvaluation = ave;

            //Calcular VI
            min = double.MaxValue;
            ave = 0;
            aux = 0;
            for (int i = 0; i < this.prob.GroundTruths.Count; i++)
            {
                aux = vi.Process(segm, this.prob.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.prob.GroundTruths.Count;
            this.prob.Best_VIEvaluation = min;
            this.prob.Ave_VIEvaluation = ave;
        }

        private Color[] colors = new Color[] { Color.White, Color.Black, Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Blue, Color.Orange, Color.Magenta, Color.Pink, Color.Gold, Color.Silver, Color.Brown, Color.Turquoise, Color.Tomato, Color.Chocolate, Color.BurlyWood, Color.Purple, Color.LemonChiffon, Color.DarkBlue, Color.SpringGreen, Color.Olive, Color.LightBlue, Color.DarkRed, Color.DarkGreen, Color.Firebrick, Color.LightGreen, Color.Cyan, Color.Salmon, Color.Honeydew, Color.Khaki, Color.LightSlateGray, Color.Tan, Color.Teal, Color.PapayaWhip, Color.PeachPuff, Color.Peru, Color.Ivory, Color.LightSalmon, Color.Gainsboro, Color.Fuchsia, Color.LavenderBlush, Color.NavajoWhite, Color.OldLace, Color.Aqua, Color.Beige, Color.Bisque, Color.Thistle, Color.Wheat, Color.SandyBrown, Color.RoyalBlue, Color.Honeydew, Color.SpringGreen, Color.Snow, Color.Goldenrod, Color.FloralWhite, Color.OldLace, Color.PaleVioletRed, Color.PaleTurquoise, Color.Sienna, Color.MediumAquamarine, Color.MediumPurple, Color.MediumBlue, Color.MediumOrchid, Color.MediumSpringGreen, Color.MintCream, Color.MistyRose, Color.Moccasin };
        //Devuelve la matriz del tamanno de la imagen final con los valores de los pixeles.
        private Bitmap GetFinalSegImage(Segmentation segm)
        {
            int[,] result = new int[this.prob.Image.Height, this.prob.Image.Width];
            for (int i = 0; i < result.GetLength(0); i++)
            {
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    result[i, j] = segm.Labels[i * result.GetLength(1) + j];
                }
            }


            Bitmap image = new Bitmap(this.prob.Image.Width, this.prob.Image.Height, this.prob.Image.PixelFormat);
            BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);
            unsafe
            {
                byte* imgPtr = (byte*)(data.Scan0);

                for (int i = 0; i < data.Height; i++)
                {
                    for (int j = 0; j < data.Width; j++)
                    {
                        imgPtr[0] = colors[Math.Min(67, result[i, j])].R;
                        imgPtr[1] = colors[Math.Min(67, result[i, j])].G;
                        imgPtr[2] = colors[Math.Min(67, result[i, j])].B;

                        imgPtr += 3;
                    }
                    imgPtr += data.Stride - data.Width * 3;
                }

                image.UnlockBits(data);

            }
            return image;
        }

        //Aqui voy a salvar en disco toda la informacion q me haga falta sobre este experimento. (imagen final, evaluacion de cada indice...)
        //Best y Ave por cada medida.
        private void PrintFinalResults()
        {
            this.prob.FinalResult.Save(this.prob.FolderName + "//ResImage_" + this.prob.ImageName + ".jpg");
            StreamWriter sw = new StreamWriter(this.prob.FolderName + "//ResTxt_" + this.prob.ImageName + ".txt", false);
            sw.WriteLine("% Measures: Rand, NMI, VI, DistConsensus first row: best GT evaluation, second row: average GT evaluation");

            //rand index
            sw.WriteLine("% Rand Index");
            sw.WriteLine(this.prob.Best_RandEvaluation.ToString());
            sw.WriteLine(this.prob.Ave_RandEvaluation.ToString());

            //nmi
            sw.WriteLine("% NMI");
            sw.WriteLine(this.prob.Best_NMIEvaluation.ToString());
            sw.WriteLine(this.prob.Ave_NMIEvaluation.ToString());

            //vi
            sw.WriteLine("% VI");
            sw.WriteLine(this.prob.Best_VIEvaluation.ToString());
            sw.WriteLine(this.prob.Ave_VIEvaluation.ToString());

            //Distance to Theoretical Consensus
            sw.WriteLine("% Distance to Theoretical consensus partition");
            sw.WriteLine(this.prob.DistToConsensus.ToString());
            sw.Close();
        }

    }



     #region Indexes

    //public abstract class SegmentationIndex
    //{
    //    public SegmentationIndex() { }

    //    //Como en esta variante muy mal programada en prob esta toda la info..incluso las segmentaciones...no hace falta mas nada.
    //    //Aqui lo q se devuelve es el arreglo con los valores de la evaluacion de cada segmentacion por el indice.
    //    protected abstract double[] Evaluate(Problem prob);

    //    public double[] Evaluations { get; set; }
    //    public double[] NormalizedEvaluations { get; set; }
    //    public double Entropy { get; set; }
    //    public double MinEvaluation { get; set; }
    //    public double MaxEvaluation { get; set; }
    //    public double SumOfEvaluations { get; set; }
    //    public double[] Weights { get; set; }

    //    public void EvaluateSegmentations(Problem prob)
    //    {
    //        Evaluations = Evaluate(prob);
    //        MinEvaluation = double.MaxValue; MaxEvaluation = 0; Entropy = 0; SumOfEvaluations = 0;
    //        double current = 0;
    //        for (int i = 0; i < Evaluations.Length; i++)
    //        {
    //            current = Evaluations[i];
    //            if (current < MinEvaluation)
    //                MinEvaluation = current;
    //            if (current > MaxEvaluation)
    //                MaxEvaluation = current;
    //            SumOfEvaluations += current;
    //        }

    //        NormalizedEvaluations = new double[Evaluations.Length];
    //        for (int i = 0; i < Evaluations.Length; i++)
    //            NormalizedEvaluations[i] = Evaluations[i] / SumOfEvaluations;

    //        for (int i = 0; i < NormalizedEvaluations.Length; i++)
    //            if (NormalizedEvaluations[i] != 0)
    //                Entropy += -1 * NormalizedEvaluations[i] * Math.Log(NormalizedEvaluations[i], 2);

    //        Weights = new double[Evaluations.Length];
    //        for (int i = 0; i < Evaluations.Length; i++)
    //            Weights[i] = Entropy * (1 - Math.Abs((Evaluations[i] - SumOfEvaluations / Evaluations.Length) / MaxEvaluation));

    //    }

    //}

    //public abstract class ShapeIndex : SegmentationIndex
    //{ 
    //}

    ////Este esta en el paper Zhang et al. aqui esta progrmada modificacion para la representacion por superpixels
    //public class CompactnessIndex : ShapeIndex
    //{
    //    protected override double[]  Evaluate(Problem prob)
    //    {
    //        double[] result = new double[prob.Segmentations.Count];
    //        int perim = 0;
    //        double compact = 0;
    //        int cont = 0;
    //        foreach (Segmentation  seg in prob.Segmentations)
    //        {
    //            int sp = 0;
    //            foreach (int cluster in seg.Clusters.Keys)
    //            {
    //                perim = 0;
    //                compact = 0;
    //                for (int i = 0; i < seg.Clusters[cluster].Count; i++)
    //                {
    //                    sp = seg.Clusters[cluster][i];
    //                    perim += (int)prob.Elements[sp][prob.Attributes.ValuesCount - 1];
    //                    for (int j = i + 1; j < seg.Clusters[cluster].Count; j++)
    //                    {
    //                        perim -= 2 * prob.SPGraph.AdjMatrix[sp, seg.Clusters[cluster][j]];
    //                    }
    //                }
    //                compact = ((double)(perim * perim)) / (4 * (double)seg.Clusters[cluster].Count);

    //               result[cont] += compact;
    //            }
    //            result[cont] /= seg.Clusters.Count;
    //            cont++;
    //        }
    //        return result;
    //    }
    //}

    ////Este esta en el paper Zhang et al. aqui esta progrmada modificacion para la representacion por superpixels
    //public class CircularityIndex : ShapeIndex
    //{
    //    protected override double[] Evaluate(Problem prob)
    //    {
    //        double[] result = new double[prob.Segmentations.Count];
    //        int perim = 0;
    //        double circularity = 0;
    //        int cont = 0;
    //        foreach (Segmentation seg in prob.Segmentations)
    //        {
    //            int sp = 0;
    //            foreach (int cluster in seg.Clusters.Keys)
    //            {
    //                perim = 0;
    //                circularity = 0;
    //                for (int i = 0; i < seg.Clusters[cluster].Count; i++)
    //                {
    //                    sp = seg.Clusters[cluster][i];
    //                    perim += (int)prob.Elements[sp][prob.Attributes.ValuesCount - 1];
    //                    for (int j = i + 1; j < seg.Clusters[cluster].Count; j++)
    //                    {
    //                        perim -= 2 * prob.SPGraph.AdjMatrix[sp, seg.Clusters[cluster][j]];
    //                    }
    //                }
    //                if (perim == 0)
    //                    perim = 1;
    //                circularity = (4 * Math.PI * 4* ((double)seg.Clusters[cluster].Count)) / ((double)(perim * perim));

    //                result[cont] += circularity;
    //            }
    //            result[cont] /= seg.Clusters.Count;
    //            cont++;
    //        }
    //        return result;
    //    }
    //}

    ////Esto es basado en la idea de connectividad q se presento en el paper PR. Contar cuantos superpixeles no tinen a su superpixel mas cercano en su misma region.
    ////El superpixel mas cercano lo calculamos como el q tiene mayor valor de uniones entre el perimetro.
    //public class ConnectivityIndex : ShapeIndex
    //{
    //    protected override double[] Evaluate(Problem prob)
    //    {
    //        int nc = 3;
    //        double[] result = new double[prob.Segmentations.Count];
    //        int cont = 0;
    //        foreach (Segmentation seg in prob.Segmentations)
    //        {
    //            for (int i = 0; i < prob.SPGraph.AdjList.Length; i++)
    //            {
    //                for (int j = 0; j < nc; j++)
    //                {
    //                    if (j < prob.SPGraph.AdjList[i].Count && seg.Labels[i] != seg.Labels[prob.SPGraph.AdjList[i][j]])
    //                        result[cont] += 1.0 / ((double)(j + 1));
    //                }
    //            }
    //            cont++;
    //        }
    //        return result;
    //    }
    //}



    //public abstract class ColorIndex : SegmentationIndex { }

    //public class ColorErrorIndex : ColorIndex
    //{
    //    protected override double[] Evaluate(Problem prob)
    //    {
    //        double[] result = new double[prob.Segmentations.Count];
                       
    //        return result;
    //    }
    //}



    #endregion



    #region Similaridad entre segmentaciones

    public abstract class SegmentationSimilarity
    {
        public abstract double Process(Segmentation s1, Segmentation s2);
    }

    public class KernelSubsetSignifSegSim : SegmentationSimilarity
    {
        public override double Process(Segmentation s1, Segmentation s2)
        {
            if (s1 == null || s2 == null)
                return 0;
            if (s1 == s2)
                return 1;
            double result = 0;
            //En cls van a estar los subconjuntos comunes en las dos particiones concatenados los nombres de los clusters por un &, ademas se guarda la cantidad de elementos del subconjunto.
            Dictionary<string, long> cls = new Dictionary<string, long>();
            for (int i = 0; i < s1.Labels.Length; i++)
            {
                if (cls.ContainsKey(s1.Labels[i] + "&" + s2.Labels[i]))
                    cls[s1.Labels[i] + "&" + s2.Labels[i]] += 1;
                else
                    cls.Add(s1.Labels[i] + "&" + s2.Labels[i], 1);
            }

            string[] aux;
            //Tamanno de los cluster que contienen al subconjunto en P1 y P2 respectivamente.
            int c1 = 0, c2 = 0;
            //Calcular para cada subconjunto el valor de similaridad asociado.
            foreach (string str in cls.Keys)
            {
                aux = str.Split('&');
                c1 = s1.Clusters[int.Parse(aux[0])].Count;
                c2 = s2.Clusters[int.Parse(aux[1])].Count;
                result += similarity(cls[str], c1, c2);
            }

            return result / Math.Sqrt(ComputeNorm(s1) * ComputeNorm(s2));

        }

        private double similarity(long subsetSize, int c1, int c2)
        {
            double num = 0;
            int k = (int)Math.Min(subsetSize, 10);
            for (int i = 1; i <= k; i++)
            {
                num += CombNk(subsetSize, i) * subsetSize * subsetSize;
            }
            return num / (c1 * c2);
            //return num / Math.Pow(2, c1 + c2);
        }


        private double CombNk(long n, int k)
        {
            double num = 1, den = 1, result = 1;
            for (int i = 1; i <= k; i++)
            {
                den *= i;
                num *= (n - i + 1);
                if (i % 10 == 0)
                {
                    result *= num / den;
                    num = 1;
                    den = 1;
                }
            }
            return result * num / den;
        }


        /// <summary>
        /// Es la funcion que calcula la relevancia que va a tener un subconjunto para el cluster al que el pertenece.
        /// </summary>
        /// <param name="subsetSize"></param>
        /// <param name="clusterSize"></param>
        /// <returns></returns>
        private double Relevance(int subsetSize, int clusterSize)
        {
            return (double)subsetSize / Math.Pow(2, (double)clusterSize);
        }


        private double ComputeNorm(Segmentation s)
        {
            double norm = 0;
            foreach (int str in s.Clusters.Keys)
                norm += similarity(s.Clusters[str].Count, s.Clusters[str].Count, s.Clusters[str].Count);
            return norm;
        }
    }


    public class RandIndexSegSim : SegmentationSimilarity
    {

        //Rand es (n00 + n11) / (n(n-1)/2), pero n00+n01+n10+n11 = n(n-1)/2. Por tanto n00+n11 = n(n-1)/2 - ((n11+n10) + (n11+n01) -2*n11). 
        //Hay una forma muy facil de calcular (n11+n10) + (n11+n01) y n11 se calcula en O(n).
        public override double Process(Segmentation s1, Segmentation s2)
        {
            double rand = 0;
            double t = 0;
            double n11_n01 = 0, n11_n10 = 0, n11=0;
            foreach (List<int> cluster in s1.Clusters.Values)
            {
                t = cluster.Count;
                n11_n10 += t * (t-1) / 2;
            }

            foreach (List<int> cluster in s2.Clusters.Values)
            {
                t = cluster.Count;
                n11_n01 += t * (t - 1) / 2;
            }

            Dictionary<string, int> cls = new Dictionary<string, int>();
            for (int i = 0; i < s1.Labels.Length; i++)
            {
                if (cls.ContainsKey(s1.Labels[i] + "&" + s2.Labels[i]))
                    cls[s1.Labels[i] + "&" + s2.Labels[i]] += 1;
                else
                    cls.Add(s1.Labels[i] + "&" + s2.Labels[i], 1);
            }

            foreach (int cluster in cls.Values)
                if (cluster >= 2)
                {
                    t = cluster;
                    n11 += t * (t - 1) / 2;
                }

                t = s1.Labels.Length;
                double pc = t * (t - 1) / 2;
            
            rand = ((double)(pc - n11_n10 - n11_n01 + 2*n11)) / ((double)pc);
            return rand;
        }
    }

    public class VISegSim : SegmentationSimilarity
    {
        public override double Process(Segmentation s1, Segmentation s2)
        {
            return Entropy(s1) + Entropy(s2) - 2 * MI(s1, s2);
        }

        private double Entropy(Segmentation s)
        {
            double entropy = 0;
            double aux = 0;
            foreach (int i in s.Clusters.Keys)
            {
                aux = (((double)s.Clusters[i].Count) / ((double)s.Labels.Length));
                entropy += aux * Math.Log(aux, 2);
            }
            return -1 * entropy;
        }
        private double MI(Segmentation s1, Segmentation s2)
        {
            double mi = 0;
            double aux = 0;
            Dictionary<string, long> cls = new Dictionary<string, long>();
            for (int i = 0; i < s1.Labels.Length; i++)
            {
                if (cls.ContainsKey(s1.Labels[i] + "&" + s2.Labels[i]))
                    cls[s1.Labels[i] + "&" + s2.Labels[i]] += 1;
                else
                    cls.Add(s1.Labels[i] + "&" + s2.Labels[i], 1);
            }

            //foreach (int cluster in cls.Values)
            //{
            //    aux = (((double)cluster) / ((double)s1.Labels.Length));
            //    if (aux != 0)
            //        mi += aux * Math.Log(aux, 2);
            //}

            foreach (int cS1 in s1.Clusters.Keys)
            {
                foreach (int cS2 in s2.Clusters.Keys)
                {
                    if (cls.ContainsKey(cS1.ToString() + "&" + cS2.ToString()))
                    {
                        aux = (((double)cls[cS1.ToString() + "&" + cS2.ToString()]) / ((double)s1.Labels.Length));
                        if (aux != 0)
                            mi += aux * Math.Log((aux * s1.Labels.Length * s1.Labels.Length) / ((double)(s1.Clusters[cS1].Count) * ((double)s2.Clusters[cS2].Count)), 2);
                    }
                }
            }
            return mi;
        }
    }

    public class NMISegSim : SegmentationSimilarity
    {
        public override double Process(Segmentation s1, Segmentation s2)
        {
            double ents = Entropy(s1) * Entropy(s2);
            if (ents != 0)
                return MI(s1, s2) / Math.Sqrt(ents);
            else
                return 1;
        }

        private double Entropy(Segmentation s)
        {
            double entropy = 0;
            double aux = 0;
            foreach (int i in s.Clusters.Keys)
            {
                aux = (((double)s.Clusters[i].Count) / ((double)s.Labels.Length));
                entropy += aux * Math.Log(aux, 2);
            }
            return -1 * entropy;
        }
        private double MI(Segmentation s1, Segmentation s2)
        {
            double mi = 0;
            double aux = 0;
            Dictionary<string, long> cls = new Dictionary<string, long>();
            for (int i = 0; i < s1.Labels.Length; i++)
            {
                if (cls.ContainsKey(s1.Labels[i] + "&" + s2.Labels[i]))
                    cls[s1.Labels[i] + "&" + s2.Labels[i]] += 1;
                else
                    cls.Add(s1.Labels[i] + "&" + s2.Labels[i], 1);
            }

            //foreach (int cluster in cls.Values)
            //{
            //    aux = (((double)cluster) / ((double)s1.Labels.Length));
            //    if (aux != 0)
            //        mi += aux * Math.Log(aux, 2);
            //}

            foreach (int cS1 in s1.Clusters.Keys)
            {
                foreach (int cS2 in s2.Clusters.Keys)
                {
                    if (cls.ContainsKey(cS1.ToString() + "&" + cS2.ToString()))
                    {
                        aux = (((double)cls[cS1.ToString() + "&" + cS2.ToString()]) / ((double)s1.Labels.Length));
                        if (aux != 0)
                            mi += aux * Math.Log((aux * s1.Labels.Length * s1.Labels.Length) / ((double)(s1.Clusters[cS1].Count) * ((double)s2.Clusters[cS2].Count)), 2);
                    }
                }
            }
            return mi;
        }
    }

    #endregion

}



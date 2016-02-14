using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Segmentation_Ensemble
{
    public class SPSegmentorKFIJO: NonHierarchical
    {
         public SPSegmentorKFIJO(Set set, Proximity diss)
            : base(set, diss)
        {
            Name = "Super-pixel Segmentor";
            ProximityType = ProximityType.Dissimilarity;
        }
        public SPSegmentorKFIJO()
            : base()
        {
            Name = "Super-pixel Segmentor";
            ProximityType = ProximityType.Dissimilarity;
        }


        List<SegmentationIndex> indexes = new List<SegmentationIndex>();
        //RandIndexSegSim rand = new RandIndexSegSim();
        KernelSubsetSignifSegSim rand = new KernelSubsetSignifSegSim();


        //Metodo principal, el q crea la estructuracion final.
        public override Structuring BuildStructuring()
        {
            Structuring result = null;

            FillSegmentationIndexList();
            //double[] weights = ComputeWeights();
            double[] weights = new double[Set.Segmentations.Count];
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
            Segmentation best = SimulatedAnnealing(normWeights, s0, 4000);
            Segmentation segPixels = GetSegmentationResult(best);
            
            this.EvaluateMatrixSegmentation(segPixels);
            this.Set.FinalResult = GetFinalSegImage(segPixels);
            PrintFinalResults();
            
            return result;
        }


        //Este metodo lo q hace es llenar la lista de indices con los indices q se van a usar en este caso.
        private void FillSegmentationIndexList()
        {
            indexes = new List<SegmentationIndex>();
            indexes.Add(new CompactnessIndex());
            indexes.Add(new CircularityIndex());
            indexes.Add(new ConnectivityIndex());
        }

        private double[] ComputeWeights()
        {
            double[] weights = new double[this.Set.Segmentations.Count];
            foreach (SegmentationIndex sidx in indexes)
                sidx.EvaluateSegmentations(this.Set);

            double min = double.MaxValue; double max = 0;
            for (int i = 0; i < this.Set.Segmentations.Count; i++)
            {
                foreach (SegmentationIndex sidx in indexes)
                    weights[i] += sidx.Weights[i];
                if (weights[i] < min)
                    min = weights[i];
                if (weights[i] > max)
                    max = weights[i];
            }

            for (int i = 0; i < weights.Length; i++)
            {
                //weights[i] = weights[i] / max;
                //Otra variante
                weights[i] = (weights[i] - min) / (max - min);
            }

            return weights;
        }

        private double Compute3thTerm(double[] weights)
        {
            double result = 0;

            for (int i = 0; i < Set.Segmentations.Count; i++)
            {
                for (int j = 0; j < Set.Segmentations.Count; j++)
                {
                    result += weights[i] * weights[j] * rand.Process(Set.Segmentations[i], Set.Segmentations[j]);
                }
            }
            return result;
        }

        private double DistConsensus(double[] normWeights, Segmentation seg)
        {
            //Sumo 1 del primer termino y 1 del tercero..solo falta restar el 2do termino de la ecuacion (10) en el paper PR.
            double result = 2;
            for (int i = 0; i < Set.Segmentations.Count; i++)
            {
                result -= 2 * normWeights[i] * rand.Process(seg, Set.Segmentations[i]);
            }
            return result;
        }

        private Segmentation GetS0(double[] normWeights)
        {
            Segmentation closest = null;
            double min = double.MaxValue, current = 0; 
            for (int i = 0; i < Set.Segmentations.Count; i++)
            {
                current = DistConsensus(normWeights, Set.Segmentations[i]);
                if (current < min)
                {
                    min = current;
                    closest = Set.Segmentations[i];
                }
            }
            if (closest.ClusterCount > this.Set.KValue)
            {
                int dif = closest.ClusterCount - Set.KValue;
                closest = closest.Join(dif, Set.SPGraph);
            }
            else if (closest.ClusterCount < Set.KValue)
            {
                int dif = Set.KValue - closest.ClusterCount;
                closest = closest.Split(dif, Set.SPGraph);
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
                next = current.GetNeighbor(Set.SPGraph);
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

            this.Set.DistToConsensus = eBest;
            return best;
        }

        //Esto convierte el objeto segmentacion en la real segmantacion haciendo el proceso contrario al calculo de los superpixels.
        //El nuevo objeto segmentacion es mucho mas grande....la cardinalidad es la cantidad de pixels y no la cantidad de superpixels.
        private Segmentation GetSegmentationResult(Segmentation best)
        {
            int[] result = new int[this.Set.SuperPixelMatrix.Length];
            for (int i = 0; i < this.Set.SuperPixelMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < this.Set.SuperPixelMatrix.GetLength(1); j++)
                {
                    int sp = this.Set.SuperPixelMatrix[i, j];
                    result[i* this.Set.SuperPixelMatrix.GetLength(1) + j] = best.Labels[sp];
                }
            }
            return new Segmentation(result);
        }

        //Evaluo la segmentacion con las medidas RandIndex, VI and NMI y dejo los resultados en el set. Evaluo contra el mejor ground-truth y el promedio contra todos.
        private void EvaluateMatrixSegmentation(Segmentation segm)
        {
            RandIndexSegSim rand = new RandIndexSegSim();
            NMISegSim nmi = new NMISegSim();
            VISegSim vi = new VISegSim();

            //Calcular el rand
            double min = double.MaxValue;
            double ave = 0;
            double aux = 0;
            for (int i = 0; i < this.Set.GroundTruths.Count; i++)
            {
                aux = 1 - rand.Process(segm, this.Set.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.Set.GroundTruths.Count;
            this.Set.Best_RandEvaluation = min;
            this.Set.Ave_RandEvaluation = ave;

            //Calcular NMI
            min = double.MaxValue;
            ave = 0;
            aux = 0;
            for (int i = 0; i < this.Set.GroundTruths.Count; i++)
            {
                aux = 1- nmi.Process(segm, this.Set.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.Set.GroundTruths.Count;
            this.Set.Best_NMIEvaluation = min;
            this.Set.Ave_NMIEvaluation = ave;

            //Calcular VI
            min = double.MaxValue;
            ave = 0;
            aux = 0;
            for (int i = 0; i < this.Set.GroundTruths.Count; i++)
            {
                aux = vi.Process(segm, this.Set.GroundTruths[i]);
                if (aux < min)
                    min = aux;
                ave += aux;
            }
            ave = ave / this.Set.GroundTruths.Count;
            this.Set.Best_VIEvaluation = min;
            this.Set.Ave_VIEvaluation = ave;
        }

        private Color[] colors = new Color[] { Color.White, Color.Black, Color.Red, Color.Blue, Color.Green, Color.Yellow, Color.Blue, Color.Orange, Color.Magenta, Color.Pink, Color.Gold, Color.Silver, Color.Brown, Color.Turquoise, Color.Tomato, Color.Chocolate, Color.BurlyWood, Color.Purple, Color.LemonChiffon, Color.DarkBlue, Color.SpringGreen, Color.Olive, Color.LightBlue, Color.DarkRed, Color.DarkGreen, Color.Firebrick, Color.LightGreen, Color.Cyan, Color.Salmon, Color.Honeydew, Color.Khaki, Color.LightSlateGray, Color.Tan, Color.Teal, Color.PapayaWhip, Color.PeachPuff, Color.Peru, Color.Ivory, Color.LightSalmon, Color.Gainsboro, Color.Fuchsia, Color.LavenderBlush, Color.NavajoWhite, Color.OldLace, Color.Aqua, Color.Beige, Color.Bisque, Color.Thistle, Color.Wheat, Color.SandyBrown, Color.RoyalBlue, Color.Honeydew, Color.SpringGreen, Color.Snow, Color.Goldenrod, Color.FloralWhite, Color.OldLace, Color.PaleVioletRed, Color.PaleTurquoise, Color.Sienna, Color.MediumAquamarine, Color.MediumPurple, Color.MediumBlue, Color.MediumOrchid, Color.MediumSpringGreen, Color.MintCream, Color.MistyRose, Color.Moccasin };
        //Devuelve la matriz del tamanno de la imagen final con los valores de los pixeles.
        private Bitmap GetFinalSegImage(Segmentation segm)
        {
            int[,] result = new int[this.Set.Image.Height, this.Set.Image.Width];
            for (int i = 0; i < result.GetLength(0); i++)
            {
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    result[i, j] = segm.Labels[i * result.GetLength(1) + j];
                }
            }


            Bitmap image = new Bitmap(this.Set.Image.Width, this.Set.Image.Height,this.Set.Image.PixelFormat);
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
            this.Set.FinalResult.Save(this.Set.FolderName + "//ResImage_" + this.Set.RelationName + ".jpg");
            StreamWriter sw = new StreamWriter(this.Set.FolderName + "//ResTxt_" + this.Set.RelationName + ".txt", false);
            sw.WriteLine("% Measures: Rand, NMI, VI, DistConsensus first row: best GT evaluation, second row: average GT evaluation");

            //rand index
            sw.WriteLine("% Rand Index");
            sw.WriteLine(this.Set.Best_RandEvaluation.ToString());
            sw.WriteLine(this.Set.Ave_RandEvaluation.ToString());

            //nmi
            sw.WriteLine("% NMI");
            sw.WriteLine(this.Set.Best_NMIEvaluation.ToString());
            sw.WriteLine(this.Set.Ave_NMIEvaluation.ToString());

            //vi
            sw.WriteLine("% VI");
            sw.WriteLine(this.Set.Best_VIEvaluation.ToString());
            sw.WriteLine(this.Set.Ave_VIEvaluation.ToString());

            //Distance to Theoretical Consensus
            sw.WriteLine("% Distance to Theoretical consensus partition");
            sw.WriteLine(this.Set.DistToConsensus.ToString());
            sw.Close();
        }
        
    }

}

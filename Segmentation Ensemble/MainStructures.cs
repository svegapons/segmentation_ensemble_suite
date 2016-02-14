using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Linq.Expressions;
using System.Collections;
using System.Drawing;

namespace Segmentation_Ensemble
{

    
    public class Set
    {
        //Nuevo pa segmentacion
        public SuperPixelGraph SPGraph { get; set; }
        public List<Segmentation> Segmentations { get; set; }
        public Bitmap Image { get; set; }
        public int[,] SuperPixelMatrix { get; set; }
        public Bitmap FinalResult { get; set; }
        public string FolderName { get; set; }
        public double DistToConsensus { get; set; }
        public int KValue { get; set; }
        //Estas segmentaciones van a ser mas grandes..(del tamanno del numero de pixels no del numero de superpixels)
        public List<Segmentation> GroundTruths { get; set; }

        //Voy a guardar en el mismo objeto set las evaluaciones del resultado contra el mejor ground-truth y el promedio contra todos.
        public double Ave_RandEvaluation { get; set; }
        public double Ave_VIEvaluation { get; set; }
        public double Ave_NMIEvaluation { get; set; }

        public double Best_RandEvaluation { get; set; }
        public double Best_VIEvaluation { get; set; }
        public double Best_NMIEvaluation { get; set; }
        //

        public List<Element> Elements { get; set; }
        public int ElementsCount {
            get
            {
                if (Elements == null)
                    throw new NullReferenceException();
                return Elements.Count;
            } 
        }
        public Attributes Attributes { get; set; }
        public ElementType ElementType { get; set; }
        public string RelationName { get; set; }

        public Set(string relationName)
        {
            if (string.IsNullOrEmpty(relationName))
                throw new ArgumentException();
            this.RelationName = relationName;
            this.Elements = new List<Element>();
        }

        public Set(string relationName, List<Element> elements, Attributes attributes)
        {
            if (string.IsNullOrEmpty(relationName) || elements == null || attributes == null)
                throw new ArgumentNullException();
            this.RelationName = relationName;
            this.Elements = elements;
            this.Attributes = attributes;

            if (!attributes.Values.Exists(a => a.AttributeType != AttributeType.Numeric))
                ElementType = ElementType.Numeric;
            else if (!attributes.Values.Exists(a => a.AttributeType != AttributeType.Nominal))
                ElementType = ElementType.Nominal;
            else if (!attributes.Values.Exists(a => a.AttributeType != AttributeType.String))
                ElementType = ElementType.String;
            else if (!attributes.Values.Exists(a => a.AttributeType != AttributeType.Date))
                ElementType = ElementType.Date;
            else //Todos no son iguales 
                ElementType = ElementType.Mixt;

            foreach (var e in elements)
            {
                e.Set = this;
                e.Attributes = Attributes;
                e.ElementType = ElementType;
            }

            CalculateNormValues();
        }

        public void AddElement(Element e)
        {
            if (e == null)
                throw new ArgumentNullException();
            else if (Attributes != null && !Attributes.Equals(e.Attributes))
                throw new Exception("Los atributos del elemento no matchean con los del conjunto");

            e.Set = this;
            Elements.Add(e);
        }

        public void Swap(int i, int j)
        {
            if (i < 0 || i >= ElementsCount || j < 0 || j >= ElementsCount)
                throw new ArgumentException("Indices fuera de rango en el Metodo SWAP de la clase Set.");

            Element temp = Elements[i];

            //El elemento j va para la posicion i
            Elements[j].Index = i;
            Elements[i] = Elements[j];

            //El elemento que estaba en la posicion j va para la posicion j
            temp.Index = j;
            Elements[j] = temp;
        }

        public Element this[int index]
        {
            get
            {
                if (index < 0 || index >= ElementsCount)
                    throw new IndexOutOfRangeException();
                return Elements[index];
            }
            set
            {
                if (index < 0 || index >= ElementsCount)
                    throw new IndexOutOfRangeException();
                Elements[index] = value;
            }
        }

        //INEFICIENTE , SE PUEDE HACER EN ORDEN CONSTANTE CON UN DICCIONARIO... Dictionary<string,Element> elements
        //FALTA IMPLEMENTAR EL SET, PERO DE LA FORMA QUE ESTA ES IMPOSIBLE
        public Element this[string name]
        {
            get
            {
                if (name == null || !Elements.Exists(e => e.Name == name))
                    throw new ArgumentException();

                return Elements.First(e => e.Name == name);
            }
            set
            {
                if (value == null || !Elements.Exists(e => e.Name == name))
                    throw new ArgumentException();

                Elements[Elements.FindIndex(e => e.Name == name)] = value;
            }
        }

        private void CalculateNormValues()
        {
            double[] normvalues = new double[Attributes.ValuesCount];

            for (int i = 0; i < Attributes.ValuesCount; i++)
            {
                if (Attributes[i].AttributeType == AttributeType.Numeric)
                {
                    double max = double.MinValue;
                    double min = double.MaxValue;

                    for (int j = 0; j < ElementsCount; j++)
                    {
                        if (!this[j].IsMissing(i))
                        {
                            if ((double)this[j][i] > max)
                                max = (double)this[j][i];
                            if ((double)this[j][i] < min)
                                min = (double)this[j][i];
                        }
                    }
                    normvalues[i] = max - min;
                }
            }

            NormValues = normvalues;
        }

        public static List<int> FindAllEquals(Element aE, List<Element> aElements, Attribute aObj)
        {
            List<int> _result = new List<int>();

            for (int i = 0; i < aElements.Count; i++)
                if (EqualsByAttribute(aE, aElements[i], aObj))
                    _result.Add(i);

            return _result;
        }
        public static bool EqualsByAttribute(Element aEi, Element aEj, Attribute aAttribute)
        {
            //Si este metodo se cambian, hay que cambiar tbn RealPartition, ya que utiliza este metodo,
            //y en caso de que dos elementos tengan el atributo por el que se esta comparando missing, eso se interpreta como que son iguales los dos elmentos.
            if (!aEi.IsMissing(aAttribute.Name) && !aEj.IsMissing(aAttribute.Name))
                switch (aAttribute.AttributeType)
                {
                    case AttributeType.Nominal:
                        if ((string)aEi[aAttribute] == (string)aEj[aAttribute])
                            return true;
                        break;
                    case AttributeType.Numeric:
                        if ((double)aEi[aAttribute] == (double)aEj[aAttribute])
                            return true;
                        break;
                    case AttributeType.String:
                        if ((string)aEi[aAttribute] == (string)aEj[aAttribute])
                            return true;
                        break;
                    case AttributeType.Date:
                        if ((DateTime)aEi[aAttribute] == (DateTime)aEj[aAttribute])
                            return true;
                        break;
                    default:
                        throw new Exception("El tipo del atributo aun no ha sido implementado, Error en el metodo Equals(Element,Element,Attribute), TabEvaluation");
                }
            else if (aEi.IsMissing(aAttribute.Name) && aEj.IsMissing(aAttribute.Name))
                return true;
            return false;
        }


        /// <summary>
        /// Valores para normalizar las disimilitudes
        /// </summary>
        public double[] NormValues { get; set; }       
      

    }


    public class Attribute
    {
        public string Name { get; set; }

        public List<object> Values { get; set; }

        public Attribute(string name, List<object> values)
        {
            if (values != null)
                this.Values = values;
            else
                this.Values = new List<object>();

            this.Name = name;

        }

        public AttributeType AttributeType { get; set; }

        //Verificar cuando el atributo es de tipo Nominal por que puede tomar varios valores 
        public int ValuesCount
        {
            get
            {
                if (AttributeType != AttributeType.Nominal)
                    return 0;
                else return Values.Count;
            }
        }



        public override bool Equals(object obj)
        {
            Attribute attr = obj as Attribute;
            if (attr == null || attr.Values == null || this.Values == null || attr.ValuesCount != this.ValuesCount || attr.Name != this.Name)
                return false;

            //Verifcar que el que tengan en True sea el mismo en los 2
            if (AttributeType != attr.AttributeType)
                return false;

            for (int i = 0; i < attr.ValuesCount; i++)
            {
                switch (AttributeType)
                {
                    case AttributeType.Nominal:
                        if ((string)attr[i] != (string)this[i])
                            return false;
                        break;

                    default:
                        throw new Exception("Nada mas tienen lista de valores los atributos de tipo nominal, error en el Equals de la clase Attribute.");
                }

            }
            return true;
        }

        public object this[int index]
        {
            get
            {
                if (index < 0 || index >= ValuesCount)
                    throw new IndexOutOfRangeException();
                return Values[index];
            }
        }


        public int Missing { get; set; }
        public int MissingPercent { get; set; }
        
        public int Distinct { get; set; }
        
        public int Unique { get; set; }
        public int UniquePercent { get; set; }

    }

    
    public class Attributes
    {
        Dictionary<string, Attribute> attributes_dic;
        public List<Attribute> Values
        {
            get
            {
                List<Attribute> result = new List<Attribute>();
                foreach (var item in attributes_dic.Values)
                {
                    result.Add(item);
                }
                return result;
            }
        }
        
        public int ValuesCount
        {
            get
            {
                if (attributes_dic == null)
                    throw new NullReferenceException();

                return attributes_dic.Values.Count;
            }
        }

        public Attributes(List<Attribute> attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException();

            this.attributes_dic = new Dictionary<string, Attribute>();

            foreach (var a in attributes)
                attributes_dic.Add(a.Name, a);
        }

        public bool ContainsAttribute(string name)
        {
            return attributes_dic.ContainsKey(name);
        }

        public Attribute this[int index]
        {
            get
            {
                if (index < 0 || index >= Values.Count)
                    throw new IndexOutOfRangeException();
                return Values[index];
            }
        }

        public Attribute this[string name]
        {
            get
            {
                if (!attributes_dic.ContainsKey(name))
                    throw new ArgumentException();
                return attributes_dic[name];
            }
        }

        public override bool Equals(object obj)
        {
            Attributes attrs = obj as Attributes;
            if (attrs == null || attrs.Values == null || this.Values == null || attrs.Values.Count != this.Values.Count)
                return false;

            for (int i = 0; i < this.Values.Count; i++)
                if (!Values[i].Equals(attrs.Values[i]))
                    return false;

            return true;
        }
    }

    
    public class Element
    {
        //ID
        public string Name { get; set; }
        int index = -1;
        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        public List<object> Values { get; set; }
        public int ValuesCount
        {
            get
            {
                if (Values == null)
                    throw new NullReferenceException();
                return Values.Count;
            }
        }
        public Set Set { get; set; }
        public ElementType ElementType { get; set; }
        public Attributes Attributes { get; set; }

        public bool HasMissing { get; set; }

        public Element(Set set, List<object> realvalues)
        {
            if (set == null || realvalues == null)
                throw new ArgumentNullException();

            this.Set = set;
            this.ElementType = set.ElementType;
            this.Attributes = set.Attributes;
            this.Values = realvalues;
            this.HasMissing = realvalues.Exists(o => o == null);
        }
        public Element(List<object> realvalues, ElementType elementType, Attributes attributes)
        {
            if (realvalues == null || attributes == null)
                throw new ArgumentNullException();

            this.Values = realvalues;
            this.ElementType = elementType;
            this.Attributes = attributes;
            this.HasMissing = realvalues.Exists(o => o == null);
        }

        //Si se utiliza este constructor hay que ponerle luego ElementType y List<Attributes>
        public Element(List<object> realvalues)
        {
            if (realvalues == null)
                throw new ArgumentNullException();
            this.Values = realvalues;
            this.HasMissing = realvalues.Exists(o => o == null);
        }
        public Element() 
        {
            this.Values = new List<object>();
        }

        public bool IsMissing(int indexAttribute)
        {
            if (indexAttribute < 0 || indexAttribute >= ValuesCount)
                throw new IndexOutOfRangeException();

            return Values[indexAttribute] == null;
        }

        public bool IsMissing(string nameAttribute)
        {
            if (!Attributes.ContainsAttribute(nameAttribute))
                throw new ArgumentException();

            return Values[Attributes.Values.FindIndex(attr => attr.Name == nameAttribute)] == null;
        }

        public object this[int index]
        {
            get
            {
                if (index < 0 || index >= ValuesCount)
                    throw new IndexOutOfRangeException();

                return Values[index];
            }
            set
            {
                if (index < 0 || index >= ValuesCount)
                    throw new IndexOutOfRangeException();

                Values[index] = value;
            }
        }

        public object this[Attribute attr]
        {
            get
            {
                int index = Attributes.Values.IndexOf(attr);

                return (index >= 0) ? Values[index] : null;

            }
        }

        public string ARFF_ToString()
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < Attributes.Values.Count; i++)
            {
                if (i > 0)
                    result.Append(",");

                if (IsMissing(i))
                    result.Append("?");
                else
                    result.Append(Values[i].ToString());
            }
            return result.ToString();
        }
    }

    
    public class Cluster
    {
        public List<Element> Elements { get; set; }
        public int ElementsCount { get { return Elements.Count; } }
        public string Name { get; set; }
        public Element Centroid { get; set; }

        public Cluster(string name)
        {
            if (name == null)
                throw new ArgumentNullException();

            this.Name = name;
            this.Elements = new List<Element>();
        }
        public Cluster(string name, List<Element> elements)
        {
            if (elements == null || name == null)
                throw new ArgumentNullException();

            this.Name = name;
            this.Elements = elements;
        }

        public double RSSK()
        {
            if (Elements == null || ElementsCount == 0)
                throw new Exception();

            else if (Centroid == null)
                UpdateCentroid();

            double result = 0;
            foreach (Element e in Elements)
                result += Math.Pow(ElementsUtilities.EuclideanDistance(e, Centroid), 2);

            return result;
        }

        public void UpdateCentroid()
        {
            if (Elements == null || Elements.Count == 0)
                throw new Exception();

            Attributes attrs = Elements[0].Attributes;
            List<object> values = new List<object>();
            for (int i = 0; i < attrs.ValuesCount; i++)
            {
                switch (attrs[i].AttributeType)
                {

                    case AttributeType.Numeric:
                        double temp = 0;
                        bool _AreAllMissing = true;
                        for (int j = 0; j < ElementsCount; j++)
                            if (!Elements[j].IsMissing(i))
                            {
                                _AreAllMissing = false;
                                temp += (double)Elements[j].Values[i];
                            }

                        if(!_AreAllMissing)
                            values.Add(temp / ElementsCount);
                        else
                            values.Add(null);
 
                        break;

                    case AttributeType.Nominal:
                        string string_toAdd1 = null;
                        int ocurrences = 0;
                        foreach (string distinct in attrs[i].Values)
                        {
                            int oc_temp = Elements.Count(e => (string)e.Values[i] == distinct);
                            if (oc_temp > ocurrences)
                            {
                                ocurrences = oc_temp;
                                string_toAdd1 = distinct;
                            }
                        }
                        values.Add(string_toAdd1);
                        break;

                    case AttributeType.String:
                        string string_toAdd2 = GetValueToAddString(i);
                        values.Add(string_toAdd2);
                        break;

                    case AttributeType.Date:
                        DateTime date_toAdd = GetValueToAddDate(i);
                        if(date_toAdd != DateTime.MinValue)
                            values.Add(date_toAdd);
                        else //Para que en el constructor de Element ponga este atributo como Missing
                            values.Add(null);

                        break;

                    default:
                        break;
                }
            }
            Element centroid = new Element(values, Elements[0].ElementType, Elements[0].Attributes);
            centroid.Index = -1;

            //Actualizar el Centroid del Elemento
            Centroid = centroid;
        }

        private string GetValueToAddString(int i)
        {
            List<string> _distinct = GetDistinct<string>(i);
            string _ValueToAdd = null;
            int _ocurrences = 0;
            foreach (string _temp in _distinct)
            {
                int _oc_temp = Elements.Count(e => ((string)e.Values[i]== _temp));
                if(_oc_temp>_ocurrences)
                {
                    _ocurrences = _oc_temp;
                    _ValueToAdd = _temp;
                }
                
            }
            return _ValueToAdd;
        }
        private DateTime GetValueToAddDate(int i)
        {
            List<DateTime> _distinct = GetDistinct<DateTime>(i);
            DateTime _ValueToAdd = DateTime.MinValue;
            int _ocurrences = 0;
            foreach (DateTime _temp in _distinct)
            {
                int _oc_temp =  OcurrencesDate(_temp,i);
                if (_oc_temp > _ocurrences)
                {
                    _ocurrences = _oc_temp;
                    _ValueToAdd = _temp;
                }

            }
            return _ValueToAdd;
        }
        private int OcurrencesDate(DateTime _temp, int i)
        {
            int _result = 0;
            foreach (Element _e in Elements)
                if (!_e.IsMissing(i))
                    if ((DateTime)_e.Values[i] == _temp)
                        _result++;
            return _result;
        }
        private List<T> GetDistinct<T>(int i)
        {
            List<T> _result = new List<T>();

            foreach (Element _e in Elements)
                if (!_e.IsMissing(i))
                    if (!_result.Contains((T)_e.Values[i]))
                        _result.Add((T)_e.Values[i]);

            return _result;
        }


        private T GetValueToAdd<T>(int i)
        {
            List<T> distinct = Elements.Select(e => (T)e.Values[i]).Distinct().ToList();
            T value_toAdd = default(T);
            int ocurrences = int.MinValue;
            foreach (T s in distinct)
            {
                int oc_temp = Elements.Count(e => ((T)e.Values[i]).Equals(s));
                if (oc_temp > ocurrences)
                {
                    ocurrences = oc_temp;
                    value_toAdd = s;
                }
            }
            return value_toAdd;
        }


        public bool HaveElement(Element e)
        {
            if (e == null)
                return false;

            return Elements.Exists(el => el.Name == e.Name);
        }

        public void AddElement(Element e)
        {
            if (e == null)
                throw new ArgumentNullException();
            else if (Elements == null)
                throw new NullReferenceException("La lista de elementos de la clase Cluster es Null");
            else if (Elements.Exists(el => el.Name == e.Name))
                throw new ArgumentException();

            Elements.Add(e);
        }

        public Element this[int index]
        {
            get
            {
                if (index < 0 || index >= ElementsCount)
                    throw new IndexOutOfRangeException();

                return Elements[index];
            }
            set
            {
                if (index < 0 || index >= ElementsCount)
                    throw new IndexOutOfRangeException();

                Elements[index] = value;
            }
        }
    }

    
    public class Partition : Structuring
    {

        public override bool BeSameCluster(Element ei, Element ej)
        {
            if (HaveUnassignedElements() && (IsUnassigned(ei) || IsUnassigned(ej)))
                return false;
            else
                return Elements[ei][0] == Elements[ej][0];
        }
        public override List<Cluster> GetCluster(Element e)
        {
            if (e == null || !Elements.ContainsKey(e))
                throw new Exception("El elemento no pertenece a la particion, error en el metodo GetCLuster de la clase Particion.");

            if (HaveUnassignedElements() && IsUnassigned(e))
                return null;
            else
                return new List<Cluster>() { Clusters[Elements[e][0]] };
        }
        public override int GetCountElementsCluster(Element ei)
        {
            if (ei == null || !Elements.ContainsKey(ei))
                throw new ArgumentException();

            string clusterName = Elements[ei][0];
            return Clusters[clusterName].ElementsCount;
        }
    }

    
    public abstract class Structuring
    {

        Dictionary<string, Cluster> clusters;
        public Dictionary<string, Cluster> Clusters
        {
            get { return clusters; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();
                clusters = value;

                elements = new Dictionary<Element, List<string>>();
                foreach (var cluster in clusters.Values)
                    foreach (var e in cluster.Elements)
                        if (!elements.ContainsKey(e))
                            elements.Add(e, new List<string>() { cluster.Name });
                        else
                            elements[e].Add(cluster.Name);

            }
        }//Nombre del Cluster ; Cluster  

        Dictionary<Element, List<string>> elements;
        public Dictionary<Element, List<string>> Elements
        {
            get { return elements; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException();

                elements = value;

                clusters = new Dictionary<string, Cluster>();

                foreach (var e in elements.Keys)
                {
                    foreach (var clusterName in elements[e])
                        if (clusters.ContainsKey(clusterName))
                            clusters[clusterName].AddElement(e);
                        else
                        {
                            Cluster c = new Cluster(clusterName);
                            c.AddElement(e);
                            clusters.Add(clusterName, c);
                        }

                }
            }
        }//Elemento ;Nombre del cluster

        public Proximity Proximity { get; set; }//Recordar al Chino q lo agregue para q pinche la NewCoas

        public List<Element> UnassignedElements { get; set; }

        public int ClustersCount
        {
            get { return Clusters.Count; }
        }

       

        public bool IsUnassigned(Element e)
        {
            if (UnassignedElements == null)
                return false;
            else return UnassignedElements.Contains(e);
        }

        public bool HaveUnassignedElements()
        {
            return !(UnassignedElements == null || UnassignedElements.Count == 0);
        }

        public void ChangeClusterName(string newname, string oldname)
        {
            Cluster c = clusters[oldname];
            clusters.Remove(oldname);
            c.Name = newname;
            clusters.Add(newname, c);
            Clusters = clusters;
        }

        public abstract bool BeSameCluster(Element ei, Element ej);

        /// <summary>
        /// Dado un elemento obtener el cluster al que pertenece dicho elemento
        /// </summary>
        /// <param name="e"></param>
        /// <returns> null: en el caso en que el elementono pertenezca a ningun cluster,
        /// es decir que este en la lista de elementos Unassigned</returns>
        public abstract List<Cluster> GetCluster(Element e);

        public abstract int GetCountElementsCluster(Element ei);

        public Cluster this[string clusterName]
        {
            get
            {
                if (!Clusters.ContainsKey(clusterName))
                    throw new ArgumentException();

                return Clusters[clusterName];
            }
            set
            {
                if (!Clusters.ContainsKey(clusterName))
                    throw new ArgumentException();

                Clusters[clusterName] = value;
            }
        }
    }

    public class RealPartitionBuilder
    {
        public static Structuring BuildRealPartition(Set aSet, Attribute aObj)
        {

            if (aObj.AttributeType == AttributeType.Nominal)
            {
                Partition _result = new Partition();
                Dictionary<string, Cluster> _dic_clusters = new Dictionary<string, Cluster>();
                Set _set = aSet;

                for (int i = 0; i < aObj.ValuesCount; i++)
                    _dic_clusters.Add((string)aObj[i], new Cluster((string)aObj[i]));

                _result.UnassignedElements = new List<Element>();
                for (int i = 0; i < _set.ElementsCount; i++)
                    if (!_set[i].IsMissing(aObj.Name))
                        _dic_clusters[(string)_set[i][aObj]].AddElement(_set[i]);
                    else
                        _result.UnassignedElements.Add(_set[i]);
                List<string> _clustersToRemove = new List<string>();
                foreach (string _key in _dic_clusters.Keys)
                    if (_dic_clusters[_key].ElementsCount == 0)
                        _clustersToRemove.Add(_key);
                foreach (string _key in _clustersToRemove)
                    _dic_clusters.Remove(_key);

                _result.Clusters = _dic_clusters;

                return _result;
            }
            else
            {
                Partition _result = new Partition();
                Dictionary<string, Cluster> _dic_clusters = new Dictionary<string, Cluster>();
                Set _set = aSet;
                List<Element> _Elements = new List<Element>();
                _set.Elements.ForEach(e => _Elements.Add(e));

                int _index = 0;
                Cluster _c = null;
                while (_Elements.Count > 0)
                {
                    List<int> _remove = Set.FindAllEquals(_Elements[0], _Elements, aObj);
                    _c = new Cluster("C-" + _index);
                    if (_remove.Count > 0 && _Elements[_remove[0]].IsMissing(aObj.Name))
                    {
                        //en caso de que se cumpla, esto solo se va a cumplir una sola vez
                        _result.UnassignedElements = new List<Element>();
                        for (int i = 0; i < _remove.Count; i++)
                        {
                            _result.UnassignedElements.Add(_Elements[_remove[i] - i]);
                            _Elements.RemoveAt(_remove[i] - i);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _remove.Count; i++)
                        {
                            _c.AddElement(_Elements[_remove[i] - i]);
                            _Elements.RemoveAt(_remove[i] - i);
                        }
                        _dic_clusters.Add(_c.Name, _c);
                        _index++;
                    }
                }
                _result.Clusters = _dic_clusters;
                return _result;
            }
        }
    }

    #region ENUMS

    //ENUMS............

    public enum AttributeType
    {
        Nominal = 0,
        Numeric = 1,
        String = 2,
        Date = 4
    }

    [FlagsAttribute]
    public enum ElementType
    {
        Nominal = 1,
        Numeric = 2,
        String = 4,
        Date = 8,
        Mixt = 15
    }
    
    [FlagsAttribute]
    public enum Property
    {
        Reflexive =1,
        Simetric = 2,
        UnNamed=4,
        TriangleInequality =8,
        Distance = 15,      // Reflexive, Simetric, UnNamed, TrianguleInnequality
        SeudoDistance = 11, // Reflexive, Simetric, TriangleInequality

        UltraMetric = 16,
        Continuity = 32

        //....

    }

    public enum ProximityType
    {
        Similarity,
        Dissimilarity,
        Both
    }

    #endregion


    //NUEVO PA SEGMENTACION
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
                for (int i = 0; i < spMatrix.GetLength(0)-1; i++)
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
                    double curr=adjMatrix[i,j];
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

    //NUEVO PA SEGMENTACION
    public class Segmentation
    {
        public int[] Labels { get; set; }
        public int ClusterCount { get; set; }
        public Dictionary<int,List<int>> Clusters { get; set; }

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
            int pos = r.Next(0, Labels.Length);

            //numero de la nueva region a seleccionar entre las posibles segun el grafo de la imagen
            int reg = r.Next(0, spg.AdjList[pos].Count);

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
            int pos=0;
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

}

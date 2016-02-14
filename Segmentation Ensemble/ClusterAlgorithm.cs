using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Segmentation_Ensemble
{
    public abstract class ClusterAlgorithm
    {
        public Set Set { get; set; }// not allow null
        public Proximity Proximity { get; set; }
        public ProximityType ProximityType { get; set; }

        public ClusterAlgorithm() { }
        public ClusterAlgorithm(Set set, Proximity diss)
        {
            if (set == null || diss == null)
                throw new ArgumentNullException("Parametro Incorrecto en el constructor de la clase ClusterAlgorithm");
            this.Set = set;
            this.Proximity = diss;
        }

        public abstract Structuring BuildStructuring();

        public Structuring Structuring { get; set; }

        public string Output { get { return "Cluster algorithm"; } set { } }

        public int ClustersCount { get; set; }


        #region IName Members

        public string Name{get;set;}

        #endregion

    }

    public abstract class NonHierarchical : ClusterAlgorithm
    {
        public NonHierarchical(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Non Hierarchical"; }
        public NonHierarchical() : base() { Name = "Non Hierarchical"; }
    }

    public abstract class Hierarchical : ClusterAlgorithm
    {
        public Hierarchical(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Hierarchical"; }
        public Hierarchical() : base() { Name = "Hierarchical"; }
    }

    // Hierarchical dividirlo en Aglomerativos y Divisivos 
    public abstract class Agglomerative : Hierarchical 
    {
        public Agglomerative(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Agglomerative"; }
        public Agglomerative() : base() { Name = "Agglomerative"; }
    }
    public abstract class AgglomerativeWithLifetime : Hierarchical
    {
        public AgglomerativeWithLifetime(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Agglomerative with Lifetime"; }
        public AgglomerativeWithLifetime() : base() { Name = "Agglomerative with Lifetime"; }

        public new int ClustersCount { get; set; }
    }
    public abstract class Divisive : Hierarchical
    {
        public Divisive(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Divisive"; }
        public Divisive() : base() { Name = "Divisive"; }
    }
    public abstract class DivisiveWithLifetime : Hierarchical
    {
        public DivisiveWithLifetime(Set set, Proximity diss)
            : base(set, diss)
        { Name = "Divisive with Lifetime"; }
        public DivisiveWithLifetime() : base() { Name = "Divisive with Lifetime"; }

        public new int ClustersCount { get; set; }
    }

}
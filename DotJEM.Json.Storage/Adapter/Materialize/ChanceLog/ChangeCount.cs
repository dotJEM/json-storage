namespace DotJEM.Json.Storage.Adapter.Materialize.Log
{
    public struct ChangeCount
    {
        public int Total => Created + Updated + Deleted;

        public int Created { get; }
        public int Updated { get; }
        public int Deleted { get;  }

        public ChangeCount(int created, int updated, int deleted)
            : this()
        {
            Created = created;
            Updated = updated;
            Deleted = deleted;
        }

        public static ChangeCount operator +(ChangeCount left, ChangeCount right)
        {
            return new ChangeCount(
                left.Created + right.Created,
                left.Updated + right.Updated,
                left.Deleted + right.Deleted);
        }

        public static implicit operator int(ChangeCount count)
        {
            return count.Total;
        }

        public bool Equals(ChangeCount other)
        {
            return Created == other.Created && Updated == other.Updated && Deleted == other.Deleted;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ChangeCount && Equals((ChangeCount) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Created;
                hashCode = (hashCode * 397) ^ Updated;
                hashCode = (hashCode * 397) ^ Deleted;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"Created: {Created}, Updated: {Updated}, Deleted: {Deleted}";
        }
    }
}
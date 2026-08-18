using ImagerAvalonia.Services.MeasurementControl;
using System;
using System.Collections;
using System.Collections.Generic;

namespace ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels
{
    // =========================
    // VALIDATED COLLECTION
    // =========================

    /// <summary>
    /// A collection that enforces parent-child type constraints at runtime.
    /// Only elements whose parent implements IContainerElement can add children.
    ///
    /// Implemented via composition (wrapping a private List&lt;T&gt;) rather than
    /// inheriting List&lt;T&gt;, because List&lt;T&gt;'s members are not virtual —
    /// a `new`-hiding override is invisible to any caller that holds a reference
    /// typed as List&lt;T&gt; (or the interface), which was silently bypassing
    /// validation everywhere except the one internal call site that happened to
    /// use the concrete type.
    /// </summary>
    public class ValidatedChildrenCollection : IList<MeasurementElementBase>
    {
        private readonly List<MeasurementElementBase> _items = new();
        private readonly MeasurementElementBase _parent;

        public ValidatedChildrenCollection(MeasurementElementBase parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }

        public MeasurementElementBase this[int index]
        {
            get => _items[index];
            set
            {
                ValidateCanAddChild(value);
                _items[index] = value;
            }
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public void Add(MeasurementElementBase child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            ValidateCanAddChild(child);
            _items.Add(child);
        }

        public void AddRange(IEnumerable<MeasurementElementBase> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            // Validate all up front so a failure partway through doesn't leave
            // the collection partially modified.
            var materialized = new List<MeasurementElementBase>(collection);
            foreach (var child in materialized)
            {
                if (child == null)
                    throw new ArgumentNullException(nameof(collection));
                ValidateCanAddChild(child);
            }
            _items.AddRange(materialized);
        }

        public void Insert(int index, MeasurementElementBase child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            ValidateCanAddChild(child);
            _items.Insert(index, child);
        }

        public void Clear() => _items.Clear();
        public bool Contains(MeasurementElementBase item) => _items.Contains(item);
        public void CopyTo(MeasurementElementBase[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<MeasurementElementBase> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int IndexOf(MeasurementElementBase item) => _items.IndexOf(item);
        public bool Remove(MeasurementElementBase item) => _items.Remove(item);
        public void RemoveAt(int index) => _items.RemoveAt(index);

        private void ValidateCanAddChild(MeasurementElementBase child)
        {
            // Only IContainerElement types can have children
            if (!(_parent is IContainerElement))
            {
                throw new InvalidOperationException(
                    $"Element of type '{_parent.GetType().Name}' cannot have children. " +
                    $"Only elements implementing IContainerElement are allowed to contain children.");
            }
        }
    }
}

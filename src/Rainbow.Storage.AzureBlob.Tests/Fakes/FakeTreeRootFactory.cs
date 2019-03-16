using System.Collections.Generic;
using System.Linq;

namespace Rainbow.Storage.AzureBlob.Tests.Fakes
{
    public class StaticTreeRootFactory : ITreeRootFactory
    {
        public static StaticTreeRootFactory Create(params TreeRoot[] treeRoots)
        {
            var result = new StaticTreeRootFactory {TreeRoots = treeRoots.ToList()};
            return result;
        }

        private IList<TreeRoot> TreeRoots { get; set; }

        public IEnumerable<TreeRoot> CreateTreeRoots()
        {
            return this.TreeRoots;
        }
    }
}
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Runtime.World
{
    public interface IH5세계배치Repository
    {
        Task<H5세계배치DefinitionData> LoadWorldLayoutAsync(
            string worldLayoutStableId, CancellationToken cancellationToken);
        Task<H5현실결속BindingData> LoadGroundingBindingAsync(
            string worldLayoutStableId, CancellationToken cancellationToken);
        Task<H5현실결속준비도Data> LoadGroundingReadinessAsync(
            string worldLayoutStableId, CancellationToken cancellationToken);
    }

    public sealed class H5세계배치Bundle
    {
        public H5세계배치DefinitionData Definition = null!;
        public H5현실결속BindingData GroundingBinding = null!;
        public H5현실결속준비도Data GroundingReadiness = null!;
    }

    public sealed class H5세계배치LoadingService
    {
        private readonly IH5세계배치Repository repository;

        public H5세계배치LoadingService(IH5세계배치Repository sourceRepository)
            => repository = sourceRepository;

        public async Task<H5세계배치Bundle> LoadAsync(
            string worldLayoutStableId, CancellationToken cancellationToken)
        {
            var definition = await repository.LoadWorldLayoutAsync(
                worldLayoutStableId, cancellationToken);
            definition.Validate();
            var binding = await repository.LoadGroundingBindingAsync(
                worldLayoutStableId, cancellationToken);
            binding.Validate(definition);
            var readiness = await repository.LoadGroundingReadinessAsync(
                worldLayoutStableId, cancellationToken);
            readiness.Validate(definition);
            return new H5세계배치Bundle
            {
                Definition = definition,
                GroundingBinding = binding,
                GroundingReadiness = readiness,
            };
        }
    }
}

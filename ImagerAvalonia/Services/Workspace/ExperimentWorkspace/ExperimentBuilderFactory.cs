using ImagerAvalonia.ViewModels.MeasurementViewModels;

namespace ImagerAvalonia.Services.Workspace;

public interface IExperimentBuilderFactory
{
    ExperimentBuilder Create();
}

public class ExperimentBuilderFactory : IExperimentBuilderFactory
{
    private readonly IMeasurementElementViewModelFactory _elementFactory;

    public ExperimentBuilderFactory(IMeasurementElementViewModelFactory elementFactory)
    {
        _elementFactory = elementFactory;
    }

    public ExperimentBuilder Create() => new ExperimentBuilder(_elementFactory);
}
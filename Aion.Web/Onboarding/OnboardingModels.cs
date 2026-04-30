using Aion.Contracts.Database;

namespace Aion.Web.Onboarding;

public enum OnboardingChoice
{
    Sample,
    Scratch
}

public record OnboardingResult(OnboardingChoice Choice, DatabaseType Engine);

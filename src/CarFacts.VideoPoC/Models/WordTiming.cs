namespace CarFacts.VideoPoC.Models;

public record WordTiming(string Word, double StartSeconds, double DurationSeconds)
{
    public double EndSeconds => StartSeconds + DurationSeconds;
}

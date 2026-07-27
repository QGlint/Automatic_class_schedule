using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Solver;

public interface IScheduleSolver
{
    ScheduleResult Solve(ScheduleProblem problem, IProgress<double>? progress = null, CancellationToken ct = default);
    ScheduleResult SolveWithLocks(ScheduleProblem problem, List<LockedLesson> locks, IProgress<double>? progress = null, CancellationToken ct = default, int relaxLevel = 0);
}

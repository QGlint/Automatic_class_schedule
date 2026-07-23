using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Solver;

public interface IScheduleSolver
{
    ScheduleResult Solve(ScheduleProblem problem);

    ScheduleResult SolveWithLocks(ScheduleProblem problem, List<LockedLesson> locks);
}

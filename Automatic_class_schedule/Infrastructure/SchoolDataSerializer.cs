using System.IO;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Infrastructure;

public static class SchoolDataSerializer
{
    private const string Magic = "ASCP";
    private const int CurrentVersion = 1;

    private enum SectionTag : byte
    {
        Settings = 0x01,
        GradeInputs = 0x02,
        Classes = 0x03,
        Teachers = 0x04,
        Subjects = 0x05,
        TeacherAssignments = 0x06,
        Requirements = 0x07,
        FixedLessons = 0x08,
        ScheduleEntries = 0x09,
        End = 0xFF
    }

    public static void Serialize(Stream stream, SchoolData data)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        writer.Write(System.Text.Encoding.ASCII.GetBytes(Magic));
        writer.Write(CurrentVersion);

        WriteSection(writer, SectionTag.Settings, w => WriteSettings(w, data.Settings));
        WriteListSection(writer, SectionTag.GradeInputs, data.GradeInputs, WriteGradeInput);
        WriteListSection(writer, SectionTag.Classes, data.Classes, WriteSchoolClass);
        WriteListSection(writer, SectionTag.Teachers, data.Teachers, WriteTeacher);
        WriteListSection(writer, SectionTag.Subjects, data.Subjects, WriteSubjectDefinition);
        WriteListSection(writer, SectionTag.TeacherAssignments, data.TeacherAssignments, WriteTeacherAssignment);
        WriteListSection(writer, SectionTag.Requirements, data.Requirements, WriteLessonRequirement);
        WriteListSection(writer, SectionTag.FixedLessons, data.FixedLessons, WriteFixedLesson);
        WriteListSection(writer, SectionTag.ScheduleEntries, data.ScheduleEntries, WriteScheduleEntry);

        writer.Write((byte)SectionTag.End);
    }

    public static SchoolData Deserialize(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadBytes(4);
        if (magic[0] != 'A' || magic[1] != 'S' || magic[2] != 'C' || magic[3] != 'P')
            throw new InvalidDataException("Not a valid ASCP project file.");

        var version = reader.ReadInt32();

        var data = new SchoolData();

        while (true)
        {
            var tag = (SectionTag)reader.ReadByte();
            if (tag == SectionTag.End) break;

            int count;
            if (tag == SectionTag.Settings)
            {
                reader.ReadInt32(); // skip -1 placeholder
                data.Settings = ReadSettings(reader);
                continue;
            }

            count = reader.ReadInt32();

            switch (tag)
            {
                case SectionTag.GradeInputs:
                    ReadList(count, reader, data.GradeInputs, ReadGradeInput);
                    break;
                case SectionTag.Classes:
                    ReadList(count, reader, data.Classes, ReadSchoolClass);
                    break;
                case SectionTag.Teachers:
                    ReadList(count, reader, data.Teachers, ReadTeacher);
                    break;
                case SectionTag.Subjects:
                    ReadList(count, reader, data.Subjects, ReadSubjectDefinition);
                    break;
                case SectionTag.TeacherAssignments:
                    ReadList(count, reader, data.TeacherAssignments, ReadTeacherAssignment);
                    break;
                case SectionTag.Requirements:
                    ReadList(count, reader, data.Requirements, ReadLessonRequirement);
                    break;
                case SectionTag.FixedLessons:
                    ReadList(count, reader, data.FixedLessons, ReadFixedLesson);
                    break;
                case SectionTag.ScheduleEntries:
                    ReadList(count, reader, data.ScheduleEntries, ReadScheduleEntry);
                    break;
            }
        }

        return data;
    }

    private static void WriteSection(BinaryWriter w, SectionTag tag, Action<BinaryWriter> writeContent)
    {
        w.Write((byte)tag);
        w.Write(-1);
        writeContent(w);
    }

    private static void WriteListSection<T>(BinaryWriter w, SectionTag tag, List<T> items, Action<BinaryWriter, T> writeItem)
    {
        w.Write((byte)tag);
        w.Write(items.Count);
        foreach (var item in items)
            writeItem(w, item);
    }

    private static void ReadList<T>(int count, BinaryReader r, List<T> target, Func<BinaryReader, T> readItem)
    {
        target.Capacity = count;
        for (int i = 0; i < count; i++)
            target.Add(readItem(r));
    }

    // ---- ScheduleSettings ----
    private static void WriteSettings(BinaryWriter w, ScheduleSettings s)
    {
        w.Write(s.DaysPerWeek);
        w.Write(s.PeriodsPerDay);
        w.Write(s.MorningPeriods);
        w.Write(s.AfternoonPeriods);
        w.Write(s.IncludeEveningSelfStudy);
        w.Write(s.EveningPeriods);
        w.Write(s.SchoolName);
    }

    private static ScheduleSettings ReadSettings(BinaryReader r)
    {
        return new ScheduleSettings
        {
            DaysPerWeek = r.ReadInt32(),
            PeriodsPerDay = r.ReadInt32(),
            MorningPeriods = r.ReadInt32(),
            AfternoonPeriods = r.ReadInt32(),
            IncludeEveningSelfStudy = r.ReadBoolean(),
            EveningPeriods = r.ReadInt32(),
            SchoolName = r.ReadString()
        };
    }

    // ---- GradeInput ----
    private static void WriteGradeInput(BinaryWriter w, GradeInput g)
    {
        w.Write(g.GradeName);
        w.Write(g.ClassCount);
    }

    private static GradeInput ReadGradeInput(BinaryReader r)
    {
        return new GradeInput
        {
            GradeName = r.ReadString(),
            ClassCount = r.ReadInt32()
        };
    }

    // ---- SchoolClass ----
    private static void WriteSchoolClass(BinaryWriter w, SchoolClass c)
    {
        w.Write(c.Id.ToByteArray());
        w.Write(c.GradeName);
        w.Write(c.ClassNumber);
        w.Write(c.Name);
    }

    private static SchoolClass ReadSchoolClass(BinaryReader r)
    {
        return new SchoolClass
        {
            Id = new Guid(r.ReadBytes(16)),
            GradeName = r.ReadString(),
            ClassNumber = r.ReadInt32(),
            Name = r.ReadString()
        };
    }

    // ---- Teacher ----
    private static void WriteTeacher(BinaryWriter w, Teacher t)
    {
        w.Write(t.Id.ToByteArray());
        w.Write(t.Name);
        w.Write(t.Subject);
        w.Write(t.Role);
    }

    private static Teacher ReadTeacher(BinaryReader r)
    {
        return new Teacher
        {
            Id = new Guid(r.ReadBytes(16)),
            Name = r.ReadString(),
            Subject = r.ReadString(),
            Role = r.ReadString()
        };
    }

    // ---- SubjectDefinition ----
    private static void WriteSubjectDefinition(BinaryWriter w, SubjectDefinition s)
    {
        w.Write(s.Id.ToByteArray());
        w.Write(s.Name);
        w.Write(s.Category);
        w.Write(s.DefaultWeeklyCount);
        w.Write(s.DistributionRule);
        w.Write(s.GradeName);
    }

    private static SubjectDefinition ReadSubjectDefinition(BinaryReader r)
    {
        return new SubjectDefinition
        {
            Id = new Guid(r.ReadBytes(16)),
            Name = r.ReadString(),
            Category = r.ReadString(),
            DefaultWeeklyCount = r.ReadInt32(),
            DistributionRule = r.ReadString(),
            GradeName = r.ReadString()
        };
    }

    // ---- TeacherAssignment ----
    private static void WriteTeacherAssignment(BinaryWriter w, TeacherAssignment a)
    {
        w.Write(a.Id.ToByteArray());
        w.Write(a.TeacherName);
        w.Write(a.Subject);
        w.Write(a.WeeklyCount);
        w.Write(a.GradeName);
        w.Write(a.ClassNumbers);
        w.Write(a.ClassNames);
        w.Write(a.DistributionRule);
        w.Write(a.PreferMorning);
        w.Write(a.AvoidLastPeriod);
    }

    private static TeacherAssignment ReadTeacherAssignment(BinaryReader r)
    {
        return new TeacherAssignment
        {
            Id = new Guid(r.ReadBytes(16)),
            TeacherName = r.ReadString(),
            Subject = r.ReadString(),
            WeeklyCount = r.ReadInt32(),
            GradeName = r.ReadString(),
            ClassNumbers = r.ReadString(),
            ClassNames = r.ReadString(),
            DistributionRule = r.ReadString(),
            PreferMorning = r.ReadBoolean(),
            AvoidLastPeriod = r.ReadBoolean()
        };
    }

    // ---- LessonRequirement ----
    private static void WriteLessonRequirement(BinaryWriter w, LessonRequirement r)
    {
        w.Write(r.Id.ToByteArray());
        w.Write(r.ClassId.ToByteArray());
        w.Write(r.TeacherId.ToByteArray());
        w.Write(r.ClassName);
        w.Write(r.TeacherName);
        w.Write(r.Subject);
        w.Write(r.WeeklyCount);
        w.Write(r.DistributionRule);
        w.Write(r.PreferMorning);
        w.Write(r.AvoidLastPeriod);
    }

    private static LessonRequirement ReadLessonRequirement(BinaryReader r)
    {
        return new LessonRequirement
        {
            Id = new Guid(r.ReadBytes(16)),
            ClassId = new Guid(r.ReadBytes(16)),
            TeacherId = new Guid(r.ReadBytes(16)),
            ClassName = r.ReadString(),
            TeacherName = r.ReadString(),
            Subject = r.ReadString(),
            WeeklyCount = r.ReadInt32(),
            DistributionRule = r.ReadString(),
            PreferMorning = r.ReadBoolean(),
            AvoidLastPeriod = r.ReadBoolean()
        };
    }

    // ---- FixedLesson ----
    private static void WriteFixedLesson(BinaryWriter w, FixedLesson f)
    {
        w.Write(f.Id.ToByteArray());
        w.Write((int)f.Scope);
        w.Write(f.ScopeValue);
        w.Write(f.DayIndex);
        w.Write(f.PeriodIndex);
        w.Write(f.Subject);
        w.Write(f.TeacherName);
        w.Write(f.ClassName);
        w.Write(f.Reason);
    }

    private static FixedLesson ReadFixedLesson(BinaryReader r)
    {
        return new FixedLesson
        {
            Id = new Guid(r.ReadBytes(16)),
            Scope = (FixedLessonScope)r.ReadInt32(),
            ScopeValue = r.ReadString(),
            DayIndex = r.ReadInt32(),
            PeriodIndex = r.ReadInt32(),
            Subject = r.ReadString(),
            TeacherName = r.ReadString(),
            ClassName = r.ReadString(),
            Reason = r.ReadString()
        };
    }

    // ---- ScheduleEntry ----
    private static void WriteScheduleEntry(BinaryWriter w, ScheduleEntry e)
    {
        w.Write(e.Id.ToByteArray());
        w.Write(e.RequirementId.ToByteArray());
        w.Write(e.ClassId.ToByteArray());
        w.Write(e.TeacherId.ToByteArray());
        w.Write(e.ClassName);
        w.Write(e.TeacherName);
        w.Write(e.Subject);
        w.Write(e.DayIndex);
        w.Write(e.PeriodIndex);
        w.Write(e.Locked);
        w.Write(e.IsFixed);
        w.Write(e.Note);
    }

    private static ScheduleEntry ReadScheduleEntry(BinaryReader r)
    {
        return new ScheduleEntry
        {
            Id = new Guid(r.ReadBytes(16)),
            RequirementId = new Guid(r.ReadBytes(16)),
            ClassId = new Guid(r.ReadBytes(16)),
            TeacherId = new Guid(r.ReadBytes(16)),
            ClassName = r.ReadString(),
            TeacherName = r.ReadString(),
            Subject = r.ReadString(),
            DayIndex = r.ReadInt32(),
            PeriodIndex = r.ReadInt32(),
            Locked = r.ReadBoolean(),
            IsFixed = r.ReadBoolean(),
            Note = r.ReadString()
        };
    }
}
using System.IO;
using Automatic_class_schedule.Models;

namespace Automatic_class_schedule.Infrastructure;

public static class SchoolDataSerializer
{
    private const string Magic = "ASCP";
    private const int LegacyVersion = 1;
    private const int DirectoryVersion = 2;

    /// <summary>超过此阈值的列表将拆分到子缓存文件</summary>
    private const int CacheSplitThreshold = 100;

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
        ProjectName = 0x0A,
        CacheRef = 0x0B,
        End = 0xFF
    }

    // ================================================================
    //  Public API — directory-based project (v2)
    // ================================================================

    /// <summary>将项目保存到 .acsproj 文件（v3）。acsprojFilePath 为 .acsproj 文件路径。</summary>
    public static void SerializeToDirectory(string acsprojFilePath, SchoolData data, string projectName)
    {
        string mainFile = acsprojFilePath;
        string cacheDir = AppPaths.GetProjectCacheDir(acsprojFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(mainFile)!);
        Directory.CreateDirectory(cacheDir);

        // 决定哪些大列表需要拆分到子缓存
        var cacheRefs = new List<(string fileName, string sectionName, int count)>();
        bool splitEntries = data.ScheduleEntries.Count > CacheSplitThreshold;

        using (var stream = File.Create(mainFile))
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(Magic));
            writer.Write(DirectoryVersion);

            // ProjectName
            WriteSection(writer, SectionTag.ProjectName, w => w.Write(projectName));

            // Settings
            WriteSection(writer, SectionTag.Settings, w => WriteSettings(w, data.Settings));
            WriteListSection(writer, SectionTag.GradeInputs, data.GradeInputs, WriteGradeInput);
            WriteListSection(writer, SectionTag.Classes, data.Classes, WriteSchoolClass);
            WriteListSection(writer, SectionTag.Teachers, data.Teachers, WriteTeacher);
            WriteListSection(writer, SectionTag.Subjects, data.Subjects, WriteSubjectDefinition);
            WriteListSection(writer, SectionTag.TeacherAssignments, data.TeacherAssignments, WriteTeacherAssignment);
            WriteListSection(writer, SectionTag.Requirements, data.Requirements, WriteLessonRequirement);
            WriteListSection(writer, SectionTag.FixedLessons, data.FixedLessons, WriteFixedLesson);

            // ScheduleEntries — 大列表拆分到子缓存
            if (splitEntries)
            {
                string cacheFile = "entries.bin";
                string cachePath = Path.Combine(cacheDir, cacheFile);
                using var cacheStream = File.Create(cachePath);
                using var cacheWriter = new BinaryWriter(cacheStream, System.Text.Encoding.UTF8);
                cacheWriter.Write(data.ScheduleEntries.Count);
                foreach (var entry in data.ScheduleEntries)
                    WriteScheduleEntry(cacheWriter, entry);

                WriteSection(writer, SectionTag.CacheRef, w =>
                {
                    w.Write("ScheduleEntries");
                    w.Write(cacheFile);
                    w.Write(data.ScheduleEntries.Count);
                });
            }
            else
            {
                WriteListSection(writer, SectionTag.ScheduleEntries, data.ScheduleEntries, WriteScheduleEntry);
            }

            writer.Write((byte)SectionTag.End);
        }

        // 清理不再使用的旧缓存文件
        CleanupOrphanedCacheFiles(cacheDir, cacheRefs, splitEntries);
    }

    /// <summary>从 .acsproj 文件加载（自动检测 v1 旧格式 / v2 目录格式 / v3 文件格式）。</summary>
    public static SchoolData DeserializeFromDirectory(string projectPath)
    {
        // v3 文件格式：.acsproj 是文件
        if (File.Exists(projectPath) && projectPath.EndsWith(".acsproj", StringComparison.OrdinalIgnoreCase))
        {
            string cacheDir = AppPaths.GetProjectCacheDir(projectPath);
            using var stream = File.OpenRead(projectPath);
            return DeserializeMainFile(stream, cacheDir);
        }

        // v2 旧目录格式：.acsproj 是目录，内含 project.acs
        if (Directory.Exists(projectPath))
        {
            string mainFile = Path.Combine(projectPath, "project.acs");
            if (File.Exists(mainFile))
            {
                string cacheDir = Path.Combine(projectPath, "cache");
                using var stream = File.OpenRead(mainFile);
                return DeserializeMainFile(stream, cacheDir);
            }
        }

        // v1 旧单文件格式
        if (File.Exists(projectPath))
        {
            using var stream = File.OpenRead(projectPath);
            return DeserializeLegacy(stream);
        }

        throw new FileNotFoundException("项目文件不存在", projectPath);
    }

    /// <summary>获取项目名称（从 .acsproj 文件快速读取，不加载全部数据）。</summary>
    public static string? ReadProjectName(string projectPath)
    {
        string? mainFile = null;

        // v3: .acsproj 文件
        if (File.Exists(projectPath) && projectPath.EndsWith(".acsproj", StringComparison.OrdinalIgnoreCase))
            mainFile = projectPath;
        // v2: 目录格式
        else if (Directory.Exists(projectPath))
        {
            var f = Path.Combine(projectPath, "project.acs");
            if (File.Exists(f)) mainFile = f;
        }

        if (mainFile == null) return null;

        using var stream = File.OpenRead(mainFile);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);
        var magic = reader.ReadBytes(4);
        if (magic[0] != 'A' || magic[1] != 'S' || magic[2] != 'C' || magic[3] != 'P')
            return null;
        int version = reader.ReadInt32();
        if (version < 2) return null;

        while (true)
        {
            var tag = (SectionTag)reader.ReadByte();
            if (tag == SectionTag.End) break;

            if (tag == SectionTag.ProjectName)
            {
                reader.ReadInt32(); // skip -1 placeholder
                return reader.ReadString();
            }

            // Skip other sections
            if (tag == SectionTag.Settings)
            {
                reader.ReadInt32(); // skip -1
                SkipSettings(reader);
            }
            else if (tag == SectionTag.CacheRef)
            {
                reader.ReadInt32(); // skip -1
                reader.ReadString(); // section name
                reader.ReadString(); // file name
                reader.ReadInt32();  // count
            }
            else
            {
                int count = reader.ReadInt32();
                SkipListSection(reader, tag, count);
            }
        }

        return null;
    }

    // ================================================================
    //  Legacy single-file API (v1) — kept for backward compatibility
    // ================================================================

    /// <summary>旧版单文件序列化（保留用于兼容旧项目文件）</summary>
    public static void Serialize(Stream stream, SchoolData data)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: false);
        writer.Write(System.Text.Encoding.ASCII.GetBytes(Magic));
        writer.Write(LegacyVersion);

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

    /// <summary>旧版单文件反序列化</summary>
    public static SchoolData Deserialize(Stream stream)
        => DeserializeLegacy(stream);

    // ================================================================
    //  Internal — v2 main file deserialization
    // ================================================================

    private static SchoolData DeserializeMainFile(Stream stream, string cacheDir)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: false);

        var magic = reader.ReadBytes(4);
        if (magic[0] != 'A' || magic[1] != 'S' || magic[2] != 'C' || magic[3] != 'P')
            throw new InvalidDataException("Not a valid ACS project file.");

        int version = reader.ReadInt32();
        if (version < 2)
            throw new InvalidDataException($"Unsupported project version: {version}");

        var data = new SchoolData();

        while (true)
        {
            var tag = (SectionTag)reader.ReadByte();
            if (tag == SectionTag.End) break;

            switch (tag)
            {
                case SectionTag.ProjectName:
                    reader.ReadInt32(); // skip -1
                    data.ProjectName = reader.ReadString();
                    break;

                case SectionTag.Settings:
                    reader.ReadInt32(); // skip -1
                    data.Settings = ReadSettings(reader);
                    break;

                case SectionTag.CacheRef:
                    reader.ReadInt32(); // skip -1
                    string sectionName = reader.ReadString();
                    string cacheFileName = reader.ReadString();
                    int cacheCount = reader.ReadInt32();
                    LoadCacheSection(data, sectionName, cacheDir, cacheFileName, cacheCount);
                    break;

                case SectionTag.GradeInputs:
                    ReadList(reader.ReadInt32(), reader, data.GradeInputs, ReadGradeInput);
                    break;
                case SectionTag.Classes:
                    ReadList(reader.ReadInt32(), reader, data.Classes, ReadSchoolClass);
                    break;
                case SectionTag.Teachers:
                    ReadList(reader.ReadInt32(), reader, data.Teachers, ReadTeacher);
                    break;
                case SectionTag.Subjects:
                    ReadList(reader.ReadInt32(), reader, data.Subjects, ReadSubjectDefinition);
                    break;
                case SectionTag.TeacherAssignments:
                    ReadList(reader.ReadInt32(), reader, data.TeacherAssignments, ReadTeacherAssignment);
                    break;
                case SectionTag.Requirements:
                    ReadList(reader.ReadInt32(), reader, data.Requirements, ReadLessonRequirement);
                    break;
                case SectionTag.FixedLessons:
                    ReadList(reader.ReadInt32(), reader, data.FixedLessons, ReadFixedLesson);
                    break;
                case SectionTag.ScheduleEntries:
                    ReadList(reader.ReadInt32(), reader, data.ScheduleEntries, ReadScheduleEntry);
                    break;

                default:
                    // Unknown section — try to skip
                    int count = reader.ReadInt32();
                    if (count == -1)
                        SkipSettings(reader);
                    else
                        SkipListSection(reader, tag, count);
                    break;
            }
        }

        return data;
    }

    private static void LoadCacheSection(SchoolData data, string sectionName, string cacheDir, string cacheFileName, int count)
    {
        string cachePath = Path.Combine(cacheDir, cacheFileName);
        if (!File.Exists(cachePath)) return;

        using var cacheStream = File.OpenRead(cachePath);
        using var cacheReader = new BinaryReader(cacheStream, System.Text.Encoding.UTF8);
        int fileCount = cacheReader.ReadInt32();

        switch (sectionName)
        {
            case "ScheduleEntries":
                data.ScheduleEntries.Capacity = fileCount;
                for (int i = 0; i < fileCount; i++)
                    data.ScheduleEntries.Add(ReadScheduleEntry(cacheReader));
                break;
        }
    }

    private static void CleanupOrphanedCacheFiles(string cacheDir, List<(string, string, int)> activeRefs, bool splitEntries)
    {
        if (!Directory.Exists(cacheDir)) return;
        var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (splitEntries) activeFiles.Add("entries.bin");
        foreach (var f in Directory.GetFiles(cacheDir))
        {
            if (!activeFiles.Contains(Path.GetFileName(f)))
            {
                try { File.Delete(f); } catch { }
            }
        }
    }

    // ================================================================
    //  Internal — v1 legacy deserialization
    // ================================================================

    private static SchoolData DeserializeLegacy(Stream stream)
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

    // ================================================================
    //  Section helpers
    // ================================================================

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

    // ================================================================
    //  Skip helpers (for unknown sections)
    // ================================================================

    private static void SkipSettings(BinaryReader r)
    {
        r.ReadInt32(); // DaysPerWeek
        r.ReadInt32(); // PeriodsPerDay
        r.ReadInt32(); // MorningPeriods
        r.ReadInt32(); // AfternoonPeriods
        r.ReadBoolean(); // IncludeEveningSelfStudy
        r.ReadInt32(); // EveningPeriods
        r.ReadString(); // SchoolName
    }

    private static void SkipListSection(BinaryReader r, SectionTag tag, int count)
    {
        for (int i = 0; i < count; i++)
        {
            switch (tag)
            {
                case SectionTag.GradeInputs:
                    r.ReadString(); r.ReadInt32();
                    break;
                case SectionTag.Classes:
                    r.ReadBytes(16); r.ReadString(); r.ReadInt32(); r.ReadString();
                    break;
                case SectionTag.Teachers:
                    r.ReadBytes(16); r.ReadString(); r.ReadString(); r.ReadString();
                    break;
                case SectionTag.Subjects:
                    r.ReadBytes(16); r.ReadString(); r.ReadString(); r.ReadInt32(); r.ReadString(); r.ReadString();
                    break;
                case SectionTag.TeacherAssignments:
                    r.ReadBytes(16); r.ReadString(); r.ReadString(); r.ReadInt32(); r.ReadString();
                    r.ReadString(); r.ReadString(); r.ReadString(); r.ReadBoolean(); r.ReadBoolean();
                    break;
                case SectionTag.Requirements:
                    r.ReadBytes(16); r.ReadBytes(16); r.ReadBytes(16); r.ReadString(); r.ReadString();
                    r.ReadString(); r.ReadInt32(); r.ReadString(); r.ReadBoolean(); r.ReadBoolean();
                    break;
                case SectionTag.FixedLessons:
                    r.ReadBytes(16); r.ReadInt32(); r.ReadString(); r.ReadInt32(); r.ReadInt32();
                    r.ReadString(); r.ReadString(); r.ReadString(); r.ReadString();
                    break;
                case SectionTag.ScheduleEntries:
                    r.ReadBytes(16); r.ReadBytes(16); r.ReadBytes(16); r.ReadBytes(16);
                    r.ReadString(); r.ReadString(); r.ReadString(); r.ReadInt32(); r.ReadInt32();
                    r.ReadBoolean(); r.ReadBoolean(); r.ReadString();
                    break;
                default:
                    return; // Can't skip unknown section
            }
        }
    }

    // ================================================================
    //  Model read/write methods
    // ================================================================

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

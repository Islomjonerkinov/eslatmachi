using System;
using System.Xml.Serialization;

namespace Eslatmachi
{
    public enum RepeatKind
    {
        None,
        Daily,
        Weekly
    }

    public class Reminder
    {
        public Reminder()
        {
            Id = Guid.NewGuid().ToString();
            Title = string.Empty;
            Notes = string.Empty;
            Enabled = true;
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public DateTime RemindAt { get; set; }
        public DateTime? SnoozeUntil { get; set; }
        public RepeatKind Repeat { get; set; }
        public bool Enabled { get; set; }
        public bool Completed { get; set; }

        [XmlIgnore]
        public bool IsActive
        {
            get { return Enabled && !Completed; }
        }

        [XmlIgnore]
        public DateTime EffectiveTime
        {
            get { return SnoozeUntil.HasValue ? SnoozeUntil.Value : RemindAt; }
        }

        [XmlIgnore]
        public string RepeatText
        {
            get
            {
                switch (Repeat)
                {
                    case RepeatKind.Daily:
                        return "Har kuni";
                    case RepeatKind.Weekly:
                        return "Har hafta";
                    default:
                        return "Bir marta";
                }
            }
        }

        [XmlIgnore]
        public string StatusText
        {
            get
            {
                if (!Enabled)
                    return "O‘chirilgan";
                if (Completed)
                    return "Bajarildi";
                if (SnoozeUntil.HasValue)
                    return "Kechiktirildi";
                return "Kutilmoqda";
            }
        }

        public void MarkCompleted(DateTime now)
        {
            SnoozeUntil = null;
            if (Repeat == RepeatKind.None)
            {
                Completed = true;
                return;
            }

            do
            {
                RemindAt = Repeat == RepeatKind.Daily
                    ? RemindAt.AddDays(1)
                    : RemindAt.AddDays(7);
            }
            while (RemindAt <= now);
        }

        public void PrepareForAlert(DateTime now, bool wasSnoozed)
        {
            SnoozeUntil = null;
            if (!wasSnoozed)
                MarkCompleted(now);
            else if (Repeat == RepeatKind.None)
                Completed = true;
        }

        public void Snooze(DateTime until)
        {
            SnoozeUntil = until;
            Completed = false;
            Enabled = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Eslatmachi
{
    public class ReminderStore
    {
        private readonly string filePath;

        public ReminderStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Eslatmachi",
                "reminders.xml"))
        {
        }

        public ReminderStore(string path)
        {
            filePath = path;
        }

        public List<Reminder> Load()
        {
            if (!File.Exists(filePath))
                return new List<Reminder>();

            XmlSerializer serializer = new XmlSerializer(typeof(List<Reminder>));
            using (FileStream stream = File.OpenRead(filePath))
            {
                List<Reminder> loaded = (List<Reminder>)serializer.Deserialize(stream);
                foreach (Reminder reminder in loaded)
                {
                    if (string.IsNullOrEmpty(reminder.Id))
                        reminder.Id = Guid.NewGuid().ToString();
                    if (reminder.Title == null)
                        reminder.Title = string.Empty;
                    if (reminder.Notes == null)
                        reminder.Notes = string.Empty;
                }
                return loaded;
            }
        }

        public void Save(IEnumerable<Reminder> reminders)
        {
            string directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
            string temporaryPath = filePath + ".tmp";
            List<Reminder> items = new List<Reminder>(reminders);
            XmlSerializer serializer = new XmlSerializer(typeof(List<Reminder>));

            using (FileStream stream = File.Create(temporaryPath))
                serializer.Serialize(stream, items);

            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, null);
            else
                File.Move(temporaryPath, filePath);
        }
    }
}

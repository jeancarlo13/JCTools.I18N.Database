using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JCTools.I18N.Database.Models
{
    /// <summary>
    /// Represents a localization record
    /// </summary>
    [Table(name: "LocalizationRecord")]
    public class LocalizationRecord
    {
        [Key]
        public virtual int Id { get; set; }
        /// <summary>
        /// Key of the localization record used for found the value
        /// </summary>
        public virtual string Key { get; set; }
        /// <summary>
        /// Name of the resource owner of the localization record
        /// </summary>
        public virtual string Resource { get; set; }
        /// <summary>
        /// The culture owner of the localization record
        /// </summary>
        public virtual string Culture { get; set; }
        /// <summary>
        /// Text to show at the user
        /// </summary>
        public virtual string Text { get; set; }
        /// <summary>
        /// Last updated of the localization
        /// </summary>
        public virtual DateTime LastUpdated { get; set; }

        public override string ToString() => Text;
    }
}

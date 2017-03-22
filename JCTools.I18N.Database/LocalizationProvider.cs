using JCTools.I18N.Database.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Reflection;

namespace JCTools.I18N.Database
{
    /// <summary>
    /// Allows get the 
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class LocalizationProvider<TContext> where TContext : DbContext
    {
        /// <summary>
        /// Context with the localization database 
        /// </summary>
        private readonly TContext _dbContext;
        /// <summary>
        /// The current http context of the request
        /// </summary>
        private readonly HttpContext _httpContext;
        /// <summary>
        /// Property of the <see cref="_dbContext"/> used for access to localization records
        /// </summary>
        private readonly PropertyInfo _localizationProperty;
        private readonly IServiceProvider _serviceProvider;
        /// <summary>
        /// The resource owner of the string localizations
        /// </summary>
        private string _currentResource;
        /// <summary>
        /// Generate a new instance
        /// </summary>
        public LocalizationProvider(TContext context,
            IServiceProvider serviceProvider)
        {
            _dbContext = context;
            _serviceProvider = serviceProvider;
            var accessor = serviceProvider.GetService(typeof(IHttpContextAccessor)) as IHttpContextAccessor;
            if (accessor == null)
                throw new InvalidOperationException($"Cannot access to {typeof(IHttpContextAccessor)}");

            _httpContext = accessor.HttpContext;
            _localizationProperty = GetLocalizationProperty(_dbContext);
        }


        /// <summary>
        /// Return the <see cref="LocalizationRecord.Text"/> related at the specific key into the current execution file,
        /// with the arguments replaced
        /// </summary>
        /// <param name="key">Key of the desired <see cref="LocalizationRecord"/></param>
        /// <param name="args">Collection of object to insert into the found text</param>
        /// <returns>The text if found the key; the received key another case </returns>
        public string this[string key, params object[] args] { get => string.Format(this[key], args); }
        /// <summary>
        /// Return the <see cref="LocalizationRecord.Text"/> related at the specific key into the current execution file
        /// </summary>
        /// <param name="key">Key of the desired <see cref="LocalizationRecord"/></param>
        /// <returns>The text if found the key; the received key another case </returns>
        public string this[string key] => GetStrings(key)?.FirstOrDefault()?.Text ?? key;
        /// Allows get the collection of <see cref="LocalizationRecord"/> that contains the specified key
        /// </summary>
        /// <param name="key">Key of the desired <see cref="LocalizationRecord"/></param>
        /// <param name="args">Collection of object to insert into the found texts</param>
        /// <returns>Collection of <see cref="LocalizationRecord"/></returns>
        public IEnumerable<LocalizationRecord> GetStrings(string key, params object[] args)
        {
            var records = GetStrings(key);
            foreach (var record in records)
                record.Text = string.Format(record.Text, args);
            return records;
        }
        /// <summary>
        /// Allows get the collection of <see cref="LocalizationRecord"/> that contains the specified key
        /// </summary>
        /// <param name="key">Key of the desired <see cref="LocalizationRecord"/></param>
        /// <returns>Collection of <see cref="LocalizationRecord"/></returns>
        public IEnumerable<LocalizationRecord> GetStrings(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            if (_localizationProperty == null)
                return Enumerable.Empty<LocalizationRecord>();
            else
            {
                var culture = GetCurrentCulture();
                if (string.IsNullOrWhiteSpace(_currentResource))
                    _currentResource = GetCurrentResourcePath();

                var resource = _currentResource;

                var value = _localizationProperty.GetValue(_dbContext);
                if (value == null)
                    return Enumerable.Empty<LocalizationRecord>();
                else
                {
                    var collection = ((IEnumerable<LocalizationRecord>)value);
                    var entities = collection.Where(r => r.Key == key && r.Resource == resource);
                    var result = entities.Where(r => r.Culture == culture.Name);

                    if (!result.Any() && culture.Parent != null)
                        result = entities.Where(r => r.Culture == culture.Parent.Name);

                    return result;
                }
            }
        }
        /// <summary>
        /// Allows get the <see cref="LocalizationProvider{TContext}"/> for the specified type
        /// </summary>
        /// <param name="resource">Resource type owner of the string localizations</param>
        /// <returns>The new generated instance</returns>
        public LocalizationProvider<TContext> GetFor(Type resource)
        {
            return new LocalizationProvider<TContext>(_dbContext, _serviceProvider)
            {
                _currentResource = resource.FullName
            };
        }
        /// <summary>
        /// Allows get the <see cref="LocalizationProvider{TContext}"/> for the specified type
        /// </summary>
        /// <param name="resource">Resource type owner of the string localizations</param>
        /// <returns>The new generated instance</returns>
        public LocalizationProvider<TContext> GetFor(string resource)
        {
            return new LocalizationProvider<TContext>(_dbContext, _serviceProvider)
            {
                _currentResource = resource
            };
        }

        /// <summary>
        /// Allows get the database context property that will allows get the data form the database
        /// </summary>
        /// <returns><see cref="PropertyInfo"/> instance with the information of access property</returns>
        private PropertyInfo GetLocalizationProperty(TContext context)
        {
            var invalidMessage = $"You should add a property {typeof(DbSet<LocalizationRecord>).Namespace}.DbSet<{typeof(LocalizationRecord).FullName}> in you database context ({typeof(TContext).FullName}).";
            var localizationModelType = typeof(LocalizationRecord);
            if (context.Model.FindEntityType(localizationModelType) == null)
            {
                var entityType = context.Model.GetEntityTypes()
                    .FirstOrDefault(e => e.ClrType.Equals(localizationModelType) || e.ClrType.GetTypeInfo().BaseType.Equals(localizationModelType));
                if (entityType == null)
                    throw new InvalidOperationException(invalidMessage);
                else
                    localizationModelType = entityType.ClrType;
            }

            var dbsetType = typeof(DbSet<>).MakeGenericType(localizationModelType);
            var property = context.GetType().GetProperties()
                           .FirstOrDefault(d => d.PropertyType.Equals(dbsetType));

            if (property == null)
                throw new InvalidOperationException(invalidMessage);
            else
                return property;
        }
        /// <summary>
        /// Gets the path of the view file currently being rendered.
        /// </summary>
        /// <returns>The path of the view file currently being rendered.</returns>
        private string GetCurrentResourcePath()
        {
            var itemsFeature = _httpContext.Features.Get<IItemsFeature>();
            var urlHelper = itemsFeature.Items[typeof(IUrlHelper)] as UrlHelper;
            var viewContext = urlHelper.ActionContext as ViewContext;
            var path = viewContext.ExecutingFilePath;
            return path;
        }
        /// <summary>
        /// Gets the System.Globalization.CultureInfo for the request to be used for formatting
        /// </summary>
        /// <returns>The System.Globalization.CultureInfo for the request to be used for formatting</returns>
        private CultureInfo GetCurrentCulture()
        {
            var rqf = _httpContext.Features.Get<IRequestCultureFeature>();
            return rqf.RequestCulture.Culture;
        }
    }

}

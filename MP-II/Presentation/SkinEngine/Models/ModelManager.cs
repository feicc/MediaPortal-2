﻿#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using MediaPortal.Core.PluginManager;

namespace Presentation.SkinEngine.Models
{
  /// <summary>
  /// Singleton class to manage loading and caching model instances.
  /// </summary>
  public class ModelManager
  {
    #region Variables

    /// <summary>
    /// Singleton instance variable.
    /// </summary>
    private static ModelManager _instance;

    /// <summary>
    /// Model cache.
    /// </summary>
    private readonly IDictionary<string, Model> _models = new Dictionary<string, Model>();

    #endregion

    /// <summary>
    /// Private constructor - this singleton class will only be instantiated by the
    /// property getter of <see cref="Instance"/>.
    /// </summary>
    private ModelManager() { }

    /// <summary>
    /// Returns the Singleton ModelManager instance.
    /// </summary>
    /// <value>The Singleton ModelManager instance.</value>
    public static ModelManager Instance
    {
      get
      {
        if (_instance == null)
          _instance = new ModelManager();
        return _instance;
      }
    }

    /// <summary>
    /// Determines whether the ModelManager did already load the specified model.
    /// </summary>
    /// <param name="assemblyName">Assembly name of the model to check.</param>
    /// <param name="className">Class name of the model to check.</param>
    /// <returns>
    /// <c>true</c> if the specified model was already load into the model cache,
    /// else <c>false</c>.
    /// </returns>
    public bool Contains(string assemblyName, string className)
    {
      return _models.ContainsKey(GetInternalModelName(assemblyName, className));
    }

    /// <summary>
    /// Returns the specified model. The model will be load from the model
    /// cache, if it was already load. Else, it will be load and stored in the
    /// model cache.
    /// </summary>
    /// <param name="assemblyName">Assembly name of the model to get or load.</param>
    /// <param name="className">Class name of the model to get or load.</param>
    /// <returns>Instance of the specified <see cref="Model"/>.</returns>
    public Model GetOrLoadModel(string assemblyName, string className)
    {
      Model result = GetModel(assemblyName, className);
      if (result == null)
      {
        result = Load(assemblyName, className);
        if (result != null)
          _models.Add(GetInternalModelName(assemblyName, className), result);
      }
      return result;
    }

    /// <summary>
    /// Returns the specified model from the model cache.
    /// </summary>
    /// <param name="assemblyName">Assembly name of the model to retrieve.</param>
    /// <param name="className">Class name of the model to retrieve.</param>
    /// <returns><see cref="Model"/> instance or <c>null</c>, if the model
    /// was not loaded yet.</returns>
    public Model GetModel(string assemblyName, string className)
    {
      return GetModelByInternalName(GetInternalModelName(assemblyName, className));
    }

    /// <summary>
    /// Returns a model by its composed name. The composed name is in the form:
    /// <code>
    /// [model.Assembly]:[model.ClassName]
    /// </code>
    /// This method is a convenience method for method
    /// <see cref="GetModel(string,string)"/>.
    /// </summary>
    /// <param name="internalName">Name of the model to retrieve. The model name is of
    /// the form <c>[model.Assembly]:[model.ClassName]</c>.</param>
    /// <returns></returns>
    public Model GetModelByInternalName(string internalName)
    {
      if (_models.ContainsKey(internalName))
        return _models[internalName];
      else
        return null;
    }

    /// <summary>
    /// Loads the specified model from disk. This method doesn't store the returned model in
    /// the internal model cache. To get a model instance from outside this class, use
    /// method <see cref="GetOrLoadModel(string,string)"/>.
    /// </summary>
    /// <param name="assemblyName">Assembly name of the model to load.</param>
    /// <param name="className">Class name of the model to load.</param>
    /// <returns>The model requested to load, else <code>null</code> if the model was not
    /// registered as expected and therefore it could not be load.</returns>
    protected static Model Load(string assemblyName, string className)
    {
      ServiceScope.Get<ILogger>().Debug("ModelManager: Load model assemblyName: {0} class: {1}", assemblyName, className);
      try
      {
        object model = ServiceScope.Get<IPluginManager>().GetPluginItem<object>("/Models/" + assemblyName, className);
        if (model != null)
        {
          Type exportedType = model.GetType();
          if (exportedType.IsClass)
          {
            return new Model(assemblyName, className, exportedType, model);
          }
        }
      }
      catch (Exception ex)
      {
        ServiceScope.Get<ILogger>().Error("ModelManager: failed to load model assemblyName: {0} class: {1}", ex, assemblyName, className);
      }
      return null;
    }

    protected static string GetInternalModelName(string assemblyName, string className)
    {
      return assemblyName + ":" + className;
    }
  }
}

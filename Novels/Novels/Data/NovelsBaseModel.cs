using PetaPoco;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using Tetr4lab;

namespace Novels.Data;

/// <summary>モデルに必要な静的プロパティ</summary>
public interface INovelsBaseModel : IBaseModel { }

/// <summary>基底モデル</summary>
[PrimaryKey ("Id", AutoIncrement = true), ExplicitColumns]
public abstract class NovelsBaseModel<T> : BaseModel<T>, IEquatable<T> where T : NovelsBaseModel<T>, new() { }

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;

namespace KontrolSage.ViewModels
{
    /// <summary>
    /// Referencia a un RegistroAvance del día de hoy, mostrado en el historial de la fila.
    /// Solo los de hoy pueden eliminarse.
    /// </summary>
    public partial class RegistroHoyViewModel : ViewModelBase
    {
        public RegistroAvance Registro { get; }
        public string FechaTexto { get; }
        public string PorcentajeTexto { get; }

        public RegistroHoyViewModel(RegistroAvance registro)
        {
            Registro = registro;
            var local = registro.FechaRegistro.ToLocalTime();
            FechaTexto = local.ToString("HH:mm:ss");
            PorcentajeTexto = registro.PorcentajeAcumulado.ToString("P2");
        }
    }

    /// <summary>
    /// Representa una fila en la vista de captura de avance.
    ///
    /// REGLAS DE NEGOCIO:
    /// 1. La captura es INCREMENTAL: ValorCaptura es un DELTA que se suma al acumulado actual.
    ///    Ej: acumulado = 0.1, usuario captura 0.2 → nuevo acumulado = 0.3
    /// 2. El avance acumulado NO puede superar 1.0 (100%). Se valida y rechaza el exceso.
    /// 3. Los registros de DÍAS ANTERIORES NO se pueden eliminar. Solo los de hoy.
    /// </summary>
    public partial class AvanceRowViewModel : ViewModelBase
    {
        // ── Datos de la actividad (readonly) ──────────────────────────────────

        public string EdtNodeId { get; }
        public string HierarchyCode { get; }
        public string Nombre { get; }
        public decimal Cantidad { get; }
        public string Unidad { get; }
        public decimal CostoTotal { get; }

        // ── Avance acumulado guardado (del último RegistroAvance en DB) ────────

        /// <summary>Porcentaje acumulado vigente (0–1). Es la base para el próximo incremento.</summary>
        [ObservableProperty]
        private decimal _porcentajeActual = 0m;

        [ObservableProperty]
        private DateTime? _fechaUltimoRegistro = null;

        // ── Registros de hoy (editables / eliminables) ────────────────────────

        [ObservableProperty]
        private ObservableCollection<RegistroHoyViewModel> _registrosDeHoy = new();

        // ── Modo de captura ───────────────────────────────────────────────────

        /// <summary>true = el usuario ingresa un incremento en porcentaje (0–1), false = en volumen.</summary>
        [ObservableProperty]
        private bool _esPorPorcentaje = true;

        // ── Valor de entrada del usuario (DELTA / INCREMENTO) ─────────────────

        private decimal _valorCaptura = 0m;

        /// <summary>
        /// Incremento a capturar:
        ///   - EsPorPorcentaje=true  → fracción directa (ej: 0.20 = 20%)
        ///   - EsPorPorcentaje=false → volumen ejecutado en esta captura (ej: 50 m³)
        /// El sistema suma este incremento al acumulado actual.
        /// </summary>
        public decimal ValorCaptura
        {
            get => _valorCaptura;
            set
            {
                if (SetProperty(ref _valorCaptura, value))
                {
                    OnPropertyChanged(nameof(IncrementoCalculado));
                    OnPropertyChanged(nameof(NuevoAcumulado));
                    OnPropertyChanged(nameof(NuevoVolumenEjecutado));
                    OnPropertyChanged(nameof(NuevoImporteEjecutado));
                    OnPropertyChanged(nameof(TieneCambio));
                    OnPropertyChanged(nameof(EsValido));
                    OnPropertyChanged(nameof(MensajeValidacion));
                    OnPropertyChanged(nameof(ExcedeLimite));
                }
            }
        }

        // ── Valores calculados ────────────────────────────────────────────────

        /// <summary>Incremento en porcentaje resultante del valor capturado (0–1).</summary>
        public decimal IncrementoCalculado
        {
            get
            {
                if (EsPorPorcentaje)
                    return Math.Max(0m, _valorCaptura);
                else
                    return Cantidad > 0 ? Math.Max(0m, _valorCaptura / Cantidad) : 0m;
            }
        }

        /// <summary>Nuevo porcentaje acumulado = actual + incremento. Máximo 1.0 (100%).</summary>
        public decimal NuevoAcumulado => Math.Min(PorcentajeActual + IncrementoCalculado, 1.0m);

        /// <summary>True si el incremento más el actual superarían el 100%.</summary>
        public bool ExcedeLimite => PorcentajeActual + IncrementoCalculado > 1.0m;

        /// <summary>Volumen ejecutado total con el nuevo acumulado.</summary>
        public decimal NuevoVolumenEjecutado => NuevoAcumulado * Cantidad;

        /// <summary>Importe ejecutado total con el nuevo acumulado.</summary>
        public decimal NuevoImporteEjecutado => NuevoAcumulado * CostoTotal;

        /// <summary>El volumen actualmente ejecutado (antes de esta captura).</summary>
        public decimal VolumenActual => PorcentajeActual * Cantidad;

        /// <summary>El importe actualmente ejecutado (antes de esta captura).</summary>
        public decimal ImporteActual => PorcentajeActual * CostoTotal;

        /// <summary>El incremento es mayor a 0 y no excede el 100%.</summary>
        public bool TieneCambio => IncrementoCalculado > 0m && !ExcedeLimite;

        /// <summary>El valor capturado es válido para guardar.</summary>
        public bool EsValido => _valorCaptura >= 0m && !ExcedeLimite;

        /// <summary>Mensaje de validación visible al usuario.</summary>
        public string MensajeValidacion
        {
            get
            {
                if (ExcedeLimite)
                {
                    decimal maxPermitido = EsPorPorcentaje
                        ? 1.0m - PorcentajeActual
                        : (1.0m - PorcentajeActual) * Cantidad;
                    return $"⚠ Excede 100%. Máximo permitido: {maxPermitido:N4} {(EsPorPorcentaje ? "(ej: " + maxPermitido.ToString("P2") + ")" : Unidad)}";
                }
                return string.Empty;
            }
        }

        /// <summary>Máximo incremento permitido en la unidad actual.</summary>
        public decimal MaxIncrementoPermitido =>
            EsPorPorcentaje ? 1.0m - PorcentajeActual : (1.0m - PorcentajeActual) * Cantidad;

        // ── Constructor ───────────────────────────────────────────────────────

        public AvanceRowViewModel(
            EdtNode nodo,
            ActividadPresupuesto actividad,
            RegistroAvance? ultimoAvance,
            System.Collections.Generic.List<RegistroAvance> registrosDeHoy)
        {
            EdtNodeId = nodo.Id ?? string.Empty;
            HierarchyCode = nodo.HierarchyCode;
            Nombre = nodo.Name;
            Cantidad = actividad.Cantidad;
            Unidad = actividad.Unidad;
            CostoTotal = actividad.CostoDirectoTotal;

            if (ultimoAvance != null)
            {
                _porcentajeActual = ultimoAvance.PorcentajeAcumulado;
                _fechaUltimoRegistro = ultimoAvance.FechaRegistro.ToLocalTime();
            }

            // Cargar historial de hoy (son los eliminables)
            foreach (var r in registrosDeHoy)
                RegistrosDeHoy.Add(new RegistroHoyViewModel(r));
        }

        // ── Reacciones ────────────────────────────────────────────────────────

        partial void OnEsPorPorcentajeChanged(bool value)
        {
            // Convertir el ValorCaptura a la nueva unidad manteniendo el mismo incremento
            var incrementoActual = IncrementoCalculado;
            _valorCaptura = value ? incrementoActual : incrementoActual * Cantidad;

            OnPropertyChanged(nameof(ValorCaptura));
            OnPropertyChanged(nameof(IncrementoCalculado));
            OnPropertyChanged(nameof(NuevoAcumulado));
            OnPropertyChanged(nameof(NuevoVolumenEjecutado));
            OnPropertyChanged(nameof(NuevoImporteEjecutado));
            OnPropertyChanged(nameof(MensajeValidacion));
            OnPropertyChanged(nameof(MaxIncrementoPermitido));
        }

        partial void OnPorcentajeActualChanged(decimal value)
        {
            OnPropertyChanged(nameof(VolumenActual));
            OnPropertyChanged(nameof(ImporteActual));
            OnPropertyChanged(nameof(TieneCambio));
            OnPropertyChanged(nameof(MaxIncrementoPermitido));
            OnPropertyChanged(nameof(MensajeValidacion));
            // Resetear captura al cambiar el acumulado (después de guardar o eliminar)
            _valorCaptura = 0m;
            OnPropertyChanged(nameof(ValorCaptura));
        }

        /// <summary>
        /// Construye el RegistroAvance con el NUEVO porcentaje acumulado.
        /// FechaRegistro la asigna MongoAvanceService.
        /// </summary>
        public RegistroAvance CrearRegistro(string projectId, int baseline, string? notas = null)
        {
            return new RegistroAvance
            {
                ProjectId = projectId,
                EdtNodeId = EdtNodeId,
                Baseline = baseline,
                PorcentajeAcumulado = NuevoAcumulado, // Ya incluye el acumulado anterior + incremento
                Notas = notas ?? string.Empty
            };
        }
    }
}

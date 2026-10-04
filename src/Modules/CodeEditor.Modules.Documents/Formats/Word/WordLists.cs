using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CodeEditor.Modules.Documents.Formats.Word;

/// <summary>
/// Lists for writing blocks: two definitions (bullets and decimal numbers) with ids after the taken ones. All bulleted
/// items share one list instance; each numbered list gets its own instance with a start number, otherwise Word would
/// continue the previous count. Definitions precede instances, as the schema requires. The numbering part is created
/// or extended on the first list item, so an insert without lists leaves the document alone.
/// </summary>
internal sealed class WordLists(MainDocumentPart main)
{
    private const int Levels = 9;
    private const int IndentStep = 360;
    private static readonly string[] Bullets = ["•", "◦", "▪"];

    private Numbering? _numbering;
    private int _bulletInstance;
    private int _orderedDefinition;
    private int _nextInstance;

    /// <summary>The bulleted list instance.</summary>
    public int BulletInstance
    {
        get
        {
            Prepare();
            return _bulletInstance;
        }
    }

    /// <summary>A new numbered list starting at <paramref name="start"/>.</summary>
    public int NewOrdered(int start)
    {
        Prepare();
        var instance = _nextInstance++;
        AddInstance(instance, _orderedDefinition, start);
        return instance;
    }

    private void Prepare()
    {
        if (_numbering is not null)
        {
            return;
        }

        var part = main.NumberingDefinitionsPart ?? main.AddNewPart<NumberingDefinitionsPart>();
        var numbering = part.Numbering ??= new Numbering();
        var nextDefinition = numbering.Elements<AbstractNum>().Select(item => item.AbstractNumberId?.Value ?? 0).DefaultIfEmpty(-1).Max() + 1;
        var nextInstance = numbering.Elements<NumberingInstance>().Select(item => item.NumberID?.Value ?? 0).DefaultIfEmpty(0).Max() + 1;
        InsertDefinitions(numbering, Definition(nextDefinition, ordered: false), Definition(nextDefinition + 1, ordered: true));
        _numbering = numbering;
        _bulletInstance = nextInstance;
        _orderedDefinition = nextDefinition + 1;
        _nextInstance = nextInstance + 1;
        AddInstance(_bulletInstance, nextDefinition, start: null);
    }

    private void AddInstance(int instance, int definition, int? start)
    {
        var item = new NumberingInstance(new AbstractNumId { Val = definition }) { NumberID = instance };
        if (start is { } value)
        {
            item.Append(new LevelOverride(new StartOverrideNumberingValue { Val = value }) { LevelIndex = 0 });
        }

        // Instances go last, but before the numIdMacAtCleanup marker from Word for Mac.
        if (_numbering!.GetFirstChild<NumberingIdMacAtCleanup>() is { } cleanup)
        {
            cleanup.InsertBeforeSelf(item);
        }
        else
        {
            _numbering.Append(item);
        }
    }

    // Definitions go after picture bullets (numPicBullet) and existing definitions, before instances.
    private static void InsertDefinitions(Numbering numbering, AbstractNum bullets, AbstractNum ordered)
    {
        var anchor = numbering.Elements<AbstractNum>().LastOrDefault<OpenXmlElement>() ?? numbering.Elements<NumberingPictureBullet>().LastOrDefault();
        if (anchor is null)
        {
            numbering.PrependChild(ordered);
            numbering.PrependChild(bullets);
            return;
        }

        anchor.InsertAfterSelf(ordered);
        anchor.InsertAfterSelf(bullets);
    }

    private static AbstractNum Definition(int id, bool ordered)
    {
        var definition = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
        for (var level = 0; level < Levels; level++)
        {
            definition.Append(new Level(
                new StartNumberingValue { Val = 1 },
                new NumberingFormat { Val = ordered ? NumberFormatValues.Decimal : NumberFormatValues.Bullet },
                new LevelText { Val = ordered ? "%" + (level + 1).ToString(CultureInfo.InvariantCulture) + "." : Bullets[level % Bullets.Length] },
                new LevelJustification { Val = LevelJustificationValues.Left },
                new PreviousParagraphProperties(new Indentation
                {
                    Left = (IndentStep * 2 * (level + 1)).ToString(CultureInfo.InvariantCulture),
                    Hanging = IndentStep.ToString(CultureInfo.InvariantCulture),
                }))
            {
                LevelIndex = level,
            });
        }

        return definition;
    }
}

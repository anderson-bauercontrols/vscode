using LanguageServer;
using LanguageServer.Client;
using LanguageServer.Parameters;
using LanguageServer.Parameters.General;
using LanguageServer.Parameters.TextDocument;
using LanguageServer.Parameters.Workspace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace VSCodeLanguageServer
{
   public class App : ServiceConnection
   {
      private Uri _workerSpaceRoot;
      private int _maxNumberOfProblems = 1000;
      private TextDocumentManager _documents;
      private Dictionary<string, ParseIB> parsedIBs = new Dictionary<string, ParseIB>();

      public App(Stream input, Stream output)
          : base(input, output)
      {
         _documents = new TextDocumentManager();
         _documents.Changed += Documents_Changed;
      }

      private void Documents_Changed(object sender, TextDocumentChangedEventArgs e)
      {
         ValidateTextDocument(e.Document);
      }

      //[JsonRpcMethod("initialize")]
      protected override Result<InitializeResult, ResponseError<InitializeErrorData>> Initialize(InitializeParams @params)
      {
         _workerSpaceRoot = @params.rootUri;
         var result = new InitializeResult
         {
            capabilities = new ServerCapabilities
            {
               hoverProvider = true,
//               definitionProvider = true,
               referencesProvider = true,
               documentHighlightProvider = true,
//               documentSymbolProvider = true,
               workspaceSymbolProvider = true,
//               codeActionProvider = true,
//               documentFormattingProvider = true,
//               documentRangeFormattingProvider = true,
//               renameProvider = true,
               textDocumentSync = TextDocumentSyncKind.Full,
               completionProvider = new CompletionOptions
               {
                  resolveProvider = true
               }
            }
         };

         return Result<InitializeResult, ResponseError<InitializeErrorData>>.Success(result);
      }

      //[JsonRpcMethod("textDocument/didOpen")]
      protected override void DidOpenTextDocument(DidOpenTextDocumentParams @params)
      {
         _documents.Add(@params.textDocument);
         parsedIBs.Add(@params.textDocument.uri.AbsolutePath, ParseIB.Parse(@params.textDocument.uri.AbsolutePath, @params.textDocument.text));
         Logger.Instance.Log($"{@params.textDocument.uri} opened.");
      }

      //[JsonRpcMethod("textDocument/didChange")]
      protected override void DidChangeTextDocument(DidChangeTextDocumentParams @params)
      {
         _documents.Change(@params.textDocument.uri, @params.textDocument.version, @params.contentChanges);
         var textDocumentItem = FindDocument(@params.textDocument.uri);
         if (parsedIBs.ContainsKey(@params.textDocument.uri.AbsolutePath))
         {
            parsedIBs[@params.textDocument.uri.AbsolutePath] = ParseIB.Parse(@params.textDocument.uri.AbsolutePath, textDocumentItem.text);
         }
         else
         {
            parsedIBs.Add(@params.textDocument.uri.AbsolutePath, ParseIB.Parse(@params.textDocument.uri.AbsolutePath, textDocumentItem.text));
         }
         Logger.Instance.Log($"{@params.textDocument.uri} changed.");
      }

      // JsonRpcMethod("textDocument/didClose")]
      protected override void DidCloseTextDocument(DidCloseTextDocumentParams @params)
      {
         _documents.Remove(@params.textDocument.uri);
         if (parsedIBs.ContainsKey(@params.textDocument.uri.AbsolutePath))
         {
            parsedIBs.Remove(@params.textDocument.uri.AbsolutePath);
         }
         Logger.Instance.Log($"{@params.textDocument.uri} closed.");
      }

      // [JsonRpcMethod("workspace/didChangeConfiguration")]
      protected override void DidChangeConfiguration(DidChangeConfigurationParams @params)
      {
         _maxNumberOfProblems = @params?.settings?.languageServerExample?.maxNumberOfProblems ?? _maxNumberOfProblems;
         Logger.Instance.Log($"maxNumberOfProblems is set to {_maxNumberOfProblems}.");
         foreach (var document in _documents.All)
         {
            ValidateTextDocument(document);
         }
      }

      private void ValidateTextDocument(TextDocumentItem document)
      {
         var diagnostics = new List<Diagnostic>();
         var lines = document.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
         var problems = 0;
         for (var i = 0; i < lines.Length && problems < _maxNumberOfProblems; i++)
         {
            var line = lines[i];
            var index = line.IndexOf("typescript");
            if (index >= 0)
            {
               problems++;
               diagnostics.Add(new Diagnostic
               {
                  severity = DiagnosticSeverity.Warning,
                  range = new Range
                  {
                     start = new Position { line = i, character = index },
                     end = new Position { line = i, character = index + 10 }
                  },
                  message = $"{line.Substring(index, 10)} should be spelled TypeScript",
                  source = "ex"
               });
            }
         }

         Proxy.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
         {
            uri = document.uri,
            diagnostics = diagnostics.ToArray()
         });
      }

      // [JsonRpcMethod("workspace/didChangeWatchedFiles")]
      protected override void DidChangeWatchedFiles(DidChangeWatchedFilesParams @params)
      {
         Logger.Instance.Log("We received an file change event");
      }

      //
      // Summary:
      //     The Completion request is sent from the client to the server to compute completion
      //     items at a given cursor position.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Completion items are presented in the IntelliSense user interface. If computing
      //     full completion items is expensive, servers can additionally provide a handler
      //     for the completion item resolve request (completionItem/resolve).
      //
      //     This request is sent when a completion item is selected in the user interface.
      //
      //
      //     A typical use case is for example: the textDocument/completion request doesn’t
      //     fill in the documentation property for returned completion items since it is
      //     expensive to compute. When the item is selected in the user interface then a
      //     completionItem/resolve request is sent with the selected completion item as a
      //     param.
      //
      //     Registration Options: CompletionRegistrationOptions
      protected override Result<CompletionResult, ResponseError> Completion(CompletionParams @params)
      {
         //@params.textDocument.uri
         //@params.position.line
         //@params.position.character
         //@params.context.triggerCharacter;
         //@params.context.triggerKind
         //CompletionTriggerKind

         TextDocumentItem textDocumentItem = FindDocument(@params.textDocument.uri);
         var array = new[]
         {
                new CompletionItem
                {
                    label = "TypeScript",
                    kind = CompletionItemKind.Text,
                    data = 1
                },
                new CompletionItem
                {
                    label = "JavaScript",
                    kind = CompletionItemKind.Text,
                    data = 2
                }
            };
         return Result<CompletionResult, ResponseError>.Success(array);
      }

      // [JsonRpcMethod("completionItem/resolve")]
      protected override Result<CompletionItem, ResponseError> ResolveCompletionItem(CompletionItem @params)
      {
         if (@params.data == 1)
         {
            @params.detail = "TypeScript details";
            @params.documentation = "TypeScript documentation";
         }
         else if (@params.data == 2)
         {
            @params.detail = "JavaScript details";
            @params.documentation = "JavaScript documentation";
         }
         return Result<CompletionItem, ResponseError>.Success(@params);
      }

      // [JsonRpcMethod("shutdown")]
      protected override VoidResult<ResponseError> Shutdown()
      {
         Logger.Instance.Log("Language Server is about to shutdown.");
         // WORKAROUND: Language Server does not receive an exit notification.
         Task.Delay(1000).ContinueWith(_ => Environment.Exit(0));
         return VoidResult<ResponseError>.Success();
      }


      // added methods

      //[JsonRpcMethod("textDocument/hover")]
      protected override Result<Hover, ResponseError> Hover(TextDocumentPositionParams @params)
      {
         //         @params.position
         //         @params.textDocument
         var textDocumentItem = FindDocument(@params.textDocument.uri);

         MarkupContent markupContent = new MarkupContent()
         {
            kind = "plaintext",
            value = string.Empty
         };
         Hover hover = new Hover
         {
            contents = new HoverContents(markupContent)
         };

         return Result<Hover, ResponseError>.Success(hover);
      }


      //[JsonRpcMethod("initialized")]
      protected override void Initialized()
      {
      }

      //[JsonRpcMethod("exit")]
      protected override void Exit()
      {
      }

      //[JsonRpcMethod("workspace/didChangeWorkspaceFolders")]
      protected override void DidChangeWorkspaceFolders(DidChangeWorkspaceFoldersParams @params)
      {
      }

      //[JsonRpcMethod("workspace/symbol")]
      protected override Result<SymbolInformation[], ResponseError> Symbol(WorkspaceSymbolParams @params)
      {
         var array = new[]
         {
            new SymbolInformation
            {
               name = string.Empty,
               kind = SymbolKind.Struct,
               location = new Location
               {
                  uri = new Uri(string.Empty)
               }
            }
         };

         return Result<SymbolInformation[], ResponseError>.Success(array);
      }

      //
      // Summary:
      //     The document symbol request is sent from the client to the server to return a
      //     flat list of all symbols found in a given text document.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Neither the symbol’s location range nor the symbol’s container name should be
      //     used to infer a hierarchy.
      //
      //     Registration Options: TextDocumentRegistrationOptions

      //[JsonRpcMethod("textDocument/documentSymbol")]
      protected override Result<DocumentSymbolResult, ResponseError> DocumentSymbols(DocumentSymbolParams @params)
      {
         TextDocumentItem foundDocument = FindDocument(@params.textDocument.uri);
         if (foundDocument != null)
         {
         }



         //         @params.textDocument

         var documentSymbolArray = new[]
         {
            new DocumentSymbol
            {

            }
         };
         DocumentSymbolResult documentSymbolResult = new DocumentSymbolResult(documentSymbolArray);
         return Result<DocumentSymbolResult, ResponseError>.Success(documentSymbolResult);

//         throw new NotImplementedException();
      }


      //[JsonRpcMethod("textDocument/signatureHelp")]
      protected override Result<SignatureHelp, ResponseError> SignatureHelp(TextDocumentPositionParams @params)
      {
         //@params.textDocument
         //@params.textDocument.uri
         //@params.position

         var textDocumentItem = FindDocument(@params.textDocument.uri);
         if (textDocumentItem != null)
         {

         }
         SignatureHelp signatureHelp = new SignatureHelp
         {

         };
         return Result<SignatureHelp, ResponseError>.Success(signatureHelp);
//         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/references")]
      protected override Result<Location[], ResponseError> FindReferences(ReferenceParams @params)
      {
         //@params.textDocument
         //@params.textDocument.uri
         //@params.position
         //@params.context
         //@params.context.includeDeclaration

         var textDocumentItem = FindDocument(@params.textDocument.uri);
         if (textDocumentItem != null)
         {

         }
         var array = new[]
         {
            new Location
            {

            }
         };
         return Result<Location[], ResponseError>.Success(array);
//         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/documentHighlight")]
      protected override Result<DocumentHighlight[], ResponseError> DocumentHighlight(TextDocumentPositionParams @params)
      {
         //@params.textDocument
         //@params.textDocument.uri
         //@params.position

         var textDocumentItem = FindDocument(@params.textDocument.uri);
         if (textDocumentItem != null)
         {

         }
         var array = new[]
         {
            new DocumentHighlight
            {

            }
         };
         return Result<DocumentHighlight[], ResponseError>.Success(array);
//         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/willSave")]
      protected override void WillSaveTextDocument(WillSaveTextDocumentParams @params)
      {
      }

      //[JsonRpcMethod("textDocument/willSaveWaitUntil")]
      protected override Result<TextEdit[], ResponseError> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/didSave")]
      protected override void DidSaveTextDocument(DidSaveTextDocumentParams @params)
      {
      }

      //
      // Summary:
      //     The document color request is sent from the client to the server to list all
      //     color references found in a given text document. Along with the range, a color
      //     value in RGB is returned.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Clients can use the result to decorate color references in an editor. For example:
      //
      //
      //     • Color boxes showing the actual color next to the reference
      //     • Show a color picker when a color reference is edited
      //[JsonRpcMethod("textDocument/documentColor")]
      protected override Result<ColorInformation[], ResponseError> DocumentColor(DocumentColorParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The color presentation request is sent from the client to the server to obtain
      //     a list of presentations for a color value at a given location.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Clients can use the result to
      //
      //     • modify a color reference.
      //     • show in a color picker and let users pick one of the presentations
      //[JsonRpcMethod("textDocument/colorPresentation")]
      protected override Result<ColorPresentation[], ResponseError> ColorPresentation(ColorPresentationParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/formatting")]
      protected override Result<TextEdit[], ResponseError> DocumentFormatting(DocumentFormattingParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/rangeFormatting")]
      protected override Result<TextEdit[], ResponseError> DocumentRangeFormatting(DocumentRangeFormattingParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/onTypeFormatting")]
      protected override Result<TextEdit[], ResponseError> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The goto definition request is sent from the client to the server to resolve
      //     the definition location of a symbol at a given text document position.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Registration Options: TextDocumentRegistrationOptions
      //[JsonRpcMethod("textDocument/definition")]
      protected override Result<LocationSingleOrArray, ResponseError> GotoDefinition(TextDocumentPositionParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The goto type definition request is sent from the client to the server to resolve
      //     the type definition location of a symbol at a given text document position.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Registration Options: TextDocumentRegistrationOptions
      //[JsonRpcMethod("textDocument/typeDefinition")]
      protected override Result<LocationSingleOrArray, ResponseError> GotoTypeDefinition(TextDocumentPositionParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The goto implementation request is sent from the client to the server to resolve
      //     the implementation location of a symbol at a given text document position.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     Registration Options: TextDocumentRegistrationOptions
      //[JsonRpcMethod("textDocument/implementation")]
      protected override Result<LocationSingleOrArray, ResponseError> GotoImplementation(TextDocumentPositionParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The code action request is sent from the client to the server to compute commands
      //     for a given text document and range.
      //
      // Parameters:
      //   params:
      //
      // Remarks:
      //     These commands are typically code fixes to either fix problems or to beautify/re factor
      //     code. The result of a textDocument/codeAction request is an array of Command
      //     literals which are typically presented in the user interface. When the command
      //     is selected the server should be contacted again (via the workspace/executeCommand
      //     request) to execute the command.
      //
      //     Since version 3.8.0: support for CodeAction literals to enable the following
      //     scenarios:
      //
      //     • the ability to directly return a workspace edit from the code action request.
      //     This avoids having another server round trip to execute an actual code action.
      //     However server providers should be aware that if the code action is expensive
      //     to compute or the edits are huge it might still be beneficial if the result is
      //     simply a command and the actual edit is only computed when needed.
      //     • the ability to group code actions using a kind. Clients are allowed to ignore
      //     that information. However it allows them to better group code action for example
      //     into corresponding menus (e.g. all re factor code actions into a re factor menu).
      //
      //
      //     Clients need to announce their support for code action literals and code action
      //     kinds via the corresponding client capability textDocument.codeAction.codeActionLiteralSupport.
      //
      //
      //     Registration Options: TextDocumentRegistrationOptions
      //[JsonRpcMethod("textDocument/codeAction")]
      protected override Result<CodeActionResult, ResponseError> CodeAction(CodeActionParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/codeLens")]
      protected override Result<CodeLens[], ResponseError> CodeLens(CodeLensParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("codeLens/resolve")]
      protected override Result<CodeLens, ResponseError> ResolveCodeLens(CodeLens @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/documentLink")]
      protected override Result<DocumentLink[], ResponseError> DocumentLink(DocumentLinkParams @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("documentLink/resolve")]
      protected override Result<DocumentLink, ResponseError> ResolveDocumentLink(DocumentLink @params)
      {
         throw new NotImplementedException();
      }

      //[JsonRpcMethod("textDocument/rename")]
      protected override Result<WorkspaceEdit, ResponseError> Rename(RenameParams @params)
      {
         throw new NotImplementedException();
      }

      //
      // Summary:
      //     The folding range request is sent from the client to the server to return all
      //     folding ranges found in a given text document.
      //
      // Parameters:
      //   params:
      //[JsonRpcMethod("textDocument/foldingRange")]
      protected override Result<FoldingRange[], ResponseError> FoldingRange(FoldingRangeRequestParam @params)
      {
         throw new NotImplementedException();
      }


      /// <summary>
      /// finds a document in the _documents list
      /// </summary>
      /// <param name="uri">document path</param>
      /// <returns>TextDocumentItem object or null</returns>
      private TextDocumentItem FindDocument(Uri uri)
      {
         foreach (var item in _documents.All)
         {
            if (item.uri == uri)
            {
               return item;
            }
         }
         return null;
      }
   }
}

function analyseCodeAnalysisResults( [Parameter(ValueFromPipeline=$true)]$codeAnalysisResultsFile ) {
  $codeAnalysisErrors = [xml](Get-Content $codeAnalysisResultsFile);

  foreach ($codeAnalysisError in $codeAnalysisErrors.SelectNodes("//Message")) {
    $issueNode = $codeAnalysisError.SelectSingleNode("Issue");
    Write-Host "Violation of Rule $($codeAnalysisError.CheckId): $($codeAnalysisError.TypeName) Line Number: $($issueNode.Line) FileName: $($issueNode.Path)\$($codeAnalysisError.Issue.File) ErrorMessage: $($issueNode.InnerXml)";
    Add-AppveyorTest "Violation of Rule $($codeAnalysisError.CheckId): $($codeAnalysisError.TypeName) Line Number: $($issueNode.Line)" -Outcome Failed -FileName "$($issueNode.Path)\$($codeAnalysisError.Issue.File)" -ErrorMessage $($issueNode.InnerXml);
  }
  
  Push-AppveyorArtifact $codeAnalysisResultsFile;
}

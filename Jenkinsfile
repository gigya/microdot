@Library("pipeline-libs@microdot") _
node('base-win'){
    dotnetLibGitHubPipeline(
        [
            projectName: "microdot",
            group: "gigya",
            dotnetVersion: "5.0.403",
            coveragePercentageThreshold: 1,
            coverageFilter: "-:*.Interface;-:*Tests*;-:type=*OrleansCodeGen*",
            releaseNugetBranches: ['main', 'master']
        ]
    )
}

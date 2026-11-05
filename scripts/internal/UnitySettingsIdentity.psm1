# Read-only fingerprint of the current entitlement identity, not a license verifier or activator.
function Read-UnityObservedBytes([string]$Path) {
    # Do not block the user's licensing client while taking an observation.
    $share = [IO.FileShare]::Read -bor [IO.FileShare]::Write -bor [IO.FileShare]::Delete
    $stream = [IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,$share)
    $memory = [IO.MemoryStream]::new()
    try { $stream.CopyTo($memory); return ,$memory.ToArray() }
    finally { $memory.Dispose(); $stream.Dispose() }
}
function Get-UnityEntitlementIdentity([string]$XmlText) {
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create([IO.StringReader]::new($XmlText),$settings)
    $document = [Xml.XmlDocument]::new()
    $document.XmlResolver = $null
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $license = $document.SelectSingleNode('/root/License')
    $groups = @($document.SelectNodes('/root/License/EntitlementGroups/EntitlementGroup'))
    if (-not $license -or -not $license.GetAttribute('Id') -or -not $license.GetAttribute('Version') -or -not $groups.Count) { throw 'Unsupported or incomplete entitlement document.' }
    foreach ($group in $groups) {
        if (-not $group.GetAttribute('Id') -or -not $group.GetAttribute('Product') -or -not $group.SelectSingleNode('Context/User/@Id') -or -not $group.SelectSingleNode('Entitlements/Entitlement/@Id')) { throw 'Incomplete entitlement identity.' }
    }
    # Unity can refresh these timestamps and the outer XML signature normally.
    # All grant/user/product/machine identifiers, rights and other validity fields remain bound.
    $license.RemoveAttribute('IssueDate')
    foreach ($node in @($license.SelectNodes('EntitlementGroups/EntitlementGroup/Context/UpdateDate'))) { $null = $node.ParentNode.RemoveChild($node) }
    function Convert-LicenseNode($Node) {
        $attributes = [ordered]@{}
        foreach ($attribute in $Node.Attributes | Sort-Object Name) { $attributes[$attribute.Name] = $attribute.Value }
        $children = @($Node.ChildNodes | Where-Object NodeType -eq ([Xml.XmlNodeType]::Element) | ForEach-Object { Convert-LicenseNode $_ } | Sort-Object -CaseSensitive)
        $text = if ($children.Count) { '' } else { $Node.InnerText }
        return ([ordered]@{tag=$Node.Name;attributes=$attributes;children=$children;text=$text} | ConvertTo-Json -Depth 8 -Compress)
    }
    $canonical = Convert-LicenseNode $license
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
}
Export-ModuleMember -Function Get-UnityEntitlementIdentity,Read-UnityObservedBytes

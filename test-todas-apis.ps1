# TESTING COMPLETO DE TODAS LAS APIS - TEXFINA
$baseUrl = "http://localhost:5116/api"
$testResults = @()
$token = ""
$totalTests = 0
$successTests = 0
$errorTests = 0
$authTests = 0

Write-Host "========================================" -ForegroundColor Green
Write-Host "   TESTING COMPLETO TEXFINA API" -ForegroundColor Green
Write-Host "   VERSION MEJORADA Y ROBUSTA" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

function TestEndpoint($name, $url, $method = "GET", $body = $null, $headers = @{}) {
    $global:totalTests++
    Write-Host "`n[$global:totalTests] Testing: $name" -ForegroundColor Yellow
    
    try {
        if ($body) {
            $response = Invoke-RestMethod -Uri $url -Method $method -Body $body -Headers $headers
        } else {
            $response = Invoke-RestMethod -Uri $url -Method $method -Headers $headers
        }
        
        Write-Host "   EXITO: $name" -ForegroundColor Green
        $global:testResults += "SUCCESS: $name"
        $global:successTests++
        return $response
    }
    catch {
        $statusCode = "Unknown"
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        
        switch ($statusCode) {
            401 { 
                Write-Host "   AUTH: $name - Requiere autenticacion" -ForegroundColor DarkYellow
                $global:testResults += "AUTH: $name"
                $global:authTests++
            }
            404 { 
                Write-Host "   NOT_FOUND: $name - Endpoint no encontrado" -ForegroundColor Red
                $global:testResults += "NOT_FOUND: $name"
                $global:errorTests++
            }
            400 { 
                Write-Host "   BAD_REQUEST: $name - Solicitud incorrecta" -ForegroundColor Red
                $global:testResults += "BAD_REQUEST: $name"
                $global:errorTests++
            }
            500 { 
                Write-Host "   SERVER_ERROR: $name - Error interno del servidor" -ForegroundColor Red
                $global:testResults += "SERVER_ERROR: $name"
                $global:errorTests++
            }
            default { 
                Write-Host "   ERROR: $name - $($_.Exception.Message)" -ForegroundColor Red
                $global:testResults += "ERROR: $name"
                $global:errorTests++
            }
        }
        return $null
    }
}

# 1. CONECTIVIDAD
Write-Host "`n1. VERIFICANDO CONECTIVIDAD" -ForegroundColor Magenta
try {
    $ping = Test-NetConnection -ComputerName "localhost" -Port 5116 -WarningAction SilentlyContinue
    if ($ping.TcpTestSucceeded) {
        Write-Host "   CONECTIVIDAD: Puerto 5116 accesible" -ForegroundColor Green
    } else {
        Write-Host "   ERROR: Puerto 5116 no accesible" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ERROR: No se pudo verificar conectividad" -ForegroundColor Red
    exit 1
}

# 2. AUTENTICACION - Probar múltiples passwords
Write-Host "`n2. AUTENTICACION" -ForegroundColor Magenta
$credenciales = @(
    @{ User = "admin"; Pass = "password" },
    @{ User = "admin"; Pass = "admin123" },
    @{ User = "admin"; Pass = "123456" }
)

foreach ($cred in $credenciales) {
    Write-Host "   Probando: $($cred.User) / $($cred.Pass)" -ForegroundColor Cyan
    $loginData = '{"Username":"' + $cred.User + '","Password":"' + $cred.Pass + '"}'
    $headers = @{"Content-Type" = "application/json"}
    
    $authResponse = TestEndpoint "Login-$($cred.User)-$($cred.Pass)" "$baseUrl/auth/login" "POST" $loginData $headers
    
    if ($authResponse -and $authResponse.token) {
        $token = $authResponse.token
        Write-Host "   LOGIN EXITOSO con: $($cred.User)/$($cred.Pass)" -ForegroundColor Green
        break
    }
}

if (-not $token) {
    Write-Host "`nCRITICO: No se pudo autenticar" -ForegroundColor Red
    exit 1
}

# Headers con autenticacion
$authHeaders = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# 3. DASHBOARD
Write-Host "`n3. DASHBOARD" -ForegroundColor Magenta
TestEndpoint "Dashboard-Resumen" "$baseUrl/dashboard/resumen" "GET" $null $authHeaders
TestEndpoint "Dashboard-Alertas" "$baseUrl/dashboard/alertas" "GET" $null $authHeaders
TestEndpoint "Dashboard-KPIs" "$baseUrl/dashboard/kpis" "GET" $null $authHeaders

# 4. ALMACENES
Write-Host "`n4. ALMACENES" -ForegroundColor Magenta
TestEndpoint "Almacenes-Lista" "$baseUrl/almacenes" "GET" $null $authHeaders
TestEndpoint "Almacenes-Estadisticas" "$baseUrl/almacenes/estadisticas" "GET" $null $authHeaders

# 5. CLASES
Write-Host "`n5. CLASES" -ForegroundColor Magenta
TestEndpoint "Clases-Lista" "$baseUrl/clases" "GET" $null $authHeaders
TestEndpoint "Clases-Jerarquia" "$baseUrl/clases/jerarquia" "GET" $null $authHeaders

# 6. UNIDADES
Write-Host "`n6. UNIDADES" -ForegroundColor Magenta
TestEndpoint "Unidades-Lista" "$baseUrl/unidades" "GET" $null $authHeaders
TestEndpoint "Unidades-MasUtilizadas" "$baseUrl/unidades/mas-utilizadas" "GET" $null $authHeaders
TestEndpoint "Unidades-Buscar" "$baseUrl/unidades/buscar?termino=kg" "GET" $null $authHeaders

# 7. PROVEEDORES
Write-Host "`n7. PROVEEDORES" -ForegroundColor Magenta
TestEndpoint "Proveedores-Lista" "$baseUrl/proveedores" "GET" $null $authHeaders
TestEndpoint "Proveedores-Estadisticas" "$baseUrl/proveedores/estadisticas" "GET" $null $authHeaders
TestEndpoint "Proveedores-Top" "$baseUrl/proveedores/top" "GET" $null $authHeaders
TestEndpoint "Proveedores-Buscar" "$baseUrl/proveedores/buscar?termino=empresa" "GET" $null $authHeaders

# 8. INSUMOS
Write-Host "`n8. INSUMOS" -ForegroundColor Magenta
TestEndpoint "Insumos-Lista" "$baseUrl/insumos" "GET" $null $authHeaders
TestEndpoint "Insumos-Buscar" "$baseUrl/insumos/buscar?termino=producto" "GET" $null $authHeaders
TestEndpoint "Insumos-Estadisticas" "$baseUrl/insumos/estadisticas" "GET" $null $authHeaders
TestEndpoint "Insumos-BajoStock" "$baseUrl/insumos/bajo-stock" "GET" $null $authHeaders

# 9. LOTES
Write-Host "`n9. LOTES" -ForegroundColor Magenta
TestEndpoint "Lotes-Lista" "$baseUrl/lotes" "GET" $null $authHeaders
TestEndpoint "Lotes-Activos" "$baseUrl/lotes/activos" "GET" $null $authHeaders
TestEndpoint "Lotes-PorVencer" "$baseUrl/lotes/por-vencer" "GET" $null $authHeaders
TestEndpoint "Lotes-Vencidos" "$baseUrl/lotes/vencidos" "GET" $null $authHeaders
TestEndpoint "Lotes-Estadisticas" "$baseUrl/lotes/estadisticas" "GET" $null $authHeaders

# 10. INGRESOS
Write-Host "`n10. INGRESOS" -ForegroundColor Magenta
TestEndpoint "Ingresos-Lista" "$baseUrl/ingresos" "GET" $null $authHeaders
TestEndpoint "Ingresos-Estadisticas" "$baseUrl/ingresos/estadisticas" "GET" $null $authHeaders
TestEndpoint "Ingresos-ConFiltros" "$baseUrl/ingresos?fechaDesde=2024-01-01" "GET" $null $authHeaders

# 11. CONSUMOS
Write-Host "`n11. CONSUMOS" -ForegroundColor Magenta
TestEndpoint "Consumos-Lista" "$baseUrl/consumos" "GET" $null $authHeaders
TestEndpoint "Consumos-Estadisticas" "$baseUrl/consumos/estadisticas" "GET" $null $authHeaders
TestEndpoint "Consumos-Areas" "$baseUrl/consumos/areas" "GET" $null $authHeaders
TestEndpoint "Consumos-PorArea" "$baseUrl/consumos?area=produccion" "GET" $null $authHeaders

# 12. STOCKS
Write-Host "`n12. STOCKS" -ForegroundColor Magenta
TestEndpoint "Stocks-Lista" "$baseUrl/stocks" "GET" $null $authHeaders
TestEndpoint "Stocks-Resumen" "$baseUrl/stocks/resumen" "GET" $null $authHeaders
TestEndpoint "Stocks-PorAlmacen" "$baseUrl/stocks/por-almacen" "GET" $null $authHeaders
TestEndpoint "Stocks-BajoMinimo" "$baseUrl/stocks/bajo-minimo" "GET" $null $authHeaders

# 13. RECETAS
Write-Host "`n13. RECETAS" -ForegroundColor Magenta
TestEndpoint "Recetas-Lista" "$baseUrl/recetas" "GET" $null $authHeaders

# 14. REPORTES
Write-Host "`n14. REPORTES" -ForegroundColor Magenta
TestEndpoint "Reportes-InventarioValorizado" "$baseUrl/reportes/inventario-valorizado" "GET" $null $authHeaders
TestEndpoint "Reportes-RotacionInventario" "$baseUrl/reportes/rotacion-inventario" "GET" $null $authHeaders
TestEndpoint "Reportes-Vencimientos" "$baseUrl/reportes/vencimientos" "GET" $null $authHeaders
TestEndpoint "Reportes-AnalisisABC" "$baseUrl/reportes/analisis-abc" "GET" $null $authHeaders
TestEndpoint "Reportes-ConsumoPorArea" "$baseUrl/reportes/consumo-por-area" "GET" $null $authHeaders
TestEndpoint "Reportes-PerformanceProveedores" "$baseUrl/reportes/performance-proveedores" "GET" $null $authHeaders

# 15. ENDPOINTS ADICIONALES
Write-Host "`n15. ENDPOINTS ADICIONALES" -ForegroundColor Magenta

# Probar Swagger UI
try {
    $swagger = Invoke-WebRequest -Uri "http://localhost:5116/swagger" -UseBasicParsing
    Write-Host "   SWAGGER UI: Accesible" -ForegroundColor Green
    $global:testResults += "SUCCESS: Swagger UI"
    $global:successTests++
    $global:totalTests++
} catch {
    Write-Host "   SWAGGER UI: No accesible" -ForegroundColor Red
    $global:testResults += "ERROR: Swagger UI"
    $global:errorTests++
    $global:totalTests++
}

# RESUMEN FINAL
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "         RESUMEN FINAL DE TESTING" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

$successRate = if ($totalTests -gt 0) { [math]::Round(($successTests / $totalTests) * 100, 1) } else { 0 }
$errorRate = if ($totalTests -gt 0) { [math]::Round(($errorTests / $totalTests) * 100, 1) } else { 0 }

Write-Host "`nRESULTADOS GENERALES:" -ForegroundColor Cyan
Write-Host "  EXITOSOS:    $successTests ($successRate%)" -ForegroundColor Green
Write-Host "  ERRORES:     $errorTests ($errorRate%)" -ForegroundColor Red
Write-Host "  AUTH REQ:    $authTests" -ForegroundColor Yellow
Write-Host "  TOTAL:       $totalTests endpoints probados" -ForegroundColor Cyan

# Clasificación del estado del sistema
Write-Host "`nESTADO DEL SISTEMA:" -ForegroundColor Cyan
if ($successRate -ge 90) {
    Write-Host "  EXCELENTE: Sistema funcionando óptimamente" -ForegroundColor Green
} elseif ($successRate -ge 75) {
    Write-Host "  BUENO: Sistema funcionando bien" -ForegroundColor Green
} elseif ($successRate -ge 50) {
    Write-Host "  REGULAR: Sistema necesita correcciones" -ForegroundColor Yellow
} else {
    Write-Host "  CRÍTICO: Sistema requiere atención urgente" -ForegroundColor Red
}

# Mostrar errores por categoría
$serverErrors = $testResults | Where-Object { $_ -like "*SERVER_ERROR*" }
$notFoundErrors = $testResults | Where-Object { $_ -like "*NOT_FOUND*" }
$badRequestErrors = $testResults | Where-Object { $_ -like "*BAD_REQUEST*" }

if ($serverErrors.Count -gt 0) {
    Write-Host "`nERRORES DE SERVIDOR (500):" -ForegroundColor Red
    $serverErrors | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
}

if ($notFoundErrors.Count -gt 0) {
    Write-Host "`nENDPOINTS NO ENCONTRADOS (404):" -ForegroundColor Yellow
    $notFoundErrors | ForEach-Object { Write-Host "     $_" -ForegroundColor Yellow }
}

if ($badRequestErrors.Count -gt 0) {
    Write-Host "`nSOLICITUDES INCORRECTAS (400):" -ForegroundColor Yellow
    $badRequestErrors | ForEach-Object { Write-Host "     $_" -ForegroundColor Yellow }
}

Write-Host "`nRECURSOS ÚTILES:" -ForegroundColor Cyan
Write-Host "  Swagger UI: http://localhost:5116/swagger" -ForegroundColor Cyan
Write-Host "  API Base: http://localhost:5116/api" -ForegroundColor Cyan

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "         TESTING COMPLETADO" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green 
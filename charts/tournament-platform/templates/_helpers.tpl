# DVO-L4-3C8B52
{{/*
Release-based base name, e.g. "tp-tournament-platform". Max 40 chars so suffixes like "-api-preview" still fit 63.
Installing the chart twice in one cluster (different release names) yields disjoint object names.
*/}}
{{- define "tournament.fullname" -}}
{{- printf "%s-%s" .Release.Name .Chart.Name | trunc 40 | trimSuffix "-" -}}
{{- end -}}

{{/*
Object name for one component. Usage: include "tournament.componentName" (dict "root" . "component" "api")
*/}}
{{- define "tournament.componentName" -}}
{{- printf "%s-%s" (include "tournament.fullname" .root) .component | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/* Common labels shared by every object of this chart (includes the department mascot label). */}}
{{- define "tournament.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" }}
app.kubernetes.io/part-of: tournament-platform
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
mascot: garbuz
{{- end -}}

{{/* Selector labels. Usage: include "tournament.selectorLabels" (dict "root" . "component" "api") */}}
{{- define "tournament.selectorLabels" -}}
app.kubernetes.io/name: {{ .root.Chart.Name }}
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .component }}
{{- end -}}

{{/* Department annotations required on every object of this work. */}}
{{- define "tournament.annotations" -}}
devops-lab/mascot-approved: "true"
devops-lab/mascot-name: "гарбуз"
{{- end -}}

{{/*
Shared API pod template. Used by BOTH templates/api-deployment.yaml and templates/api-rollout.yaml,
so switching between Deployment and Rollout cannot drift apart.
*/}}
{{- define "tournament.apiPodTemplate" -}}
metadata:
  labels:
    {{- include "tournament.labels" . | nindent 4 }}
    {{- include "tournament.selectorLabels" (dict "root" . "component" "api") | nindent 4 }}
  annotations:
    {{- include "tournament.annotations" . | nindent 4 }}
spec:
  containers:
    - name: api
      image: "{{ .Values.api.image.repository }}:{{ .Values.api.image.tag }}"
      imagePullPolicy: {{ .Values.api.image.pullPolicy }}
      ports:
        - name: http
          containerPort: 80
      env:
        - name: APP_VERSION
          value: {{ .Values.api.image.tag | toString | quote }}
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "config") }}
              key: ASPNETCORE_ENVIRONMENT
        - name: FRONTEND_ORIGIN
          valueFrom:
            configMapKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "config") }}
              key: FRONTEND_ORIGIN
        - name: JWT__ISSUER
          valueFrom:
            configMapKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "config") }}
              key: JWT__ISSUER
        - name: POSTGRES_USER
          valueFrom:
            configMapKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "config") }}
              key: POSTGRES_USER
        - name: POSTGRES_DB
          valueFrom:
            configMapKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "config") }}
              key: POSTGRES_DB
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "secrets") }}
              key: POSTGRES_PASSWORD
        - name: JWT__KEY
          valueFrom:
            secretKeyRef:
              name: {{ include "tournament.componentName" (dict "root" . "component" "secrets") }}
              key: JWT__KEY
        # Demo fault injection. Lives in the pod template on purpose: changing it creates a new rollout revision.
        - name: FAIL_RATE
          value: {{ .Values.api.failRate | toString | quote }}
        # $(VAR) is expanded by Kubernetes from the variables declared above.
        - name: ConnectionStrings__DefaultConnection
          value: "Server={{ include "tournament.componentName" (dict "root" . "component" "postgres") }};Port=5432;Database=$(POSTGRES_DB);User Id=$(POSTGRES_USER);Password=$(POSTGRES_PASSWORD);"
      resources:
        {{- toYaml .Values.api.resources | nindent 8 }}
      livenessProbe:
        httpGet:
          path: /version
          port: http
        initialDelaySeconds: 20
        periodSeconds: 15
      readinessProbe:
        httpGet:
          path: /health
          port: http
        initialDelaySeconds: 10
        periodSeconds: 10
{{- end -}}

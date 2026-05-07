<script setup lang="ts">
import { CheckCircle2, Loader2 } from 'lucide-vue-next'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '@/api/client'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import { formatTenantRole } from '@/lib/display'

const router = useRouter()
const { state, refreshTenants, switchTenant } = useAuth()
const selectedTenantId = ref(state.tenants[0]?.id)
const error = ref<ApiError | null>(null)

onMounted(async () => {
  try {
    await refreshTenants()
    selectedTenantId.value = state.currentTenant?.id || state.tenants[0]?.id
  }
  catch (tenantError) {
    error.value = tenantError instanceof ApiError
      ? tenantError
      : new ApiError(0, '租户列表加载失败，请稍后重试。')
  }
})

async function submitTenant() {
  if (!selectedTenantId.value) {
    return
  }

  error.value = null

  try {
    await switchTenant(selectedTenantId.value)
    await router.replace('/assets')
  }
  catch (switchError) {
    error.value = switchError instanceof ApiError
      ? switchError
      : new ApiError(0, '切换租户失败，请稍后重试。')
  }
}
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-background px-4 py-10">
    <Card class="w-full max-w-2xl">
      <CardHeader>
        <CardTitle class="text-xl">
          选择当前租户
        </CardTitle>
        <p class="text-sm text-muted-foreground">
          业务接口会使用登录态中的当前租户，不需要在页面请求里传入租户 ID。
        </p>
      </CardHeader>
      <CardContent>
        <form class="space-y-5" @submit.prevent="submitTenant">
          <div class="grid gap-3">
            <label
              v-for="tenant in state.tenants"
              :key="tenant.id"
              class="flex cursor-pointer items-center justify-between rounded-md border p-4 transition hover:bg-accent"
              :class="selectedTenantId === tenant.id ? 'border-primary bg-primary/5' : ''"
            >
              <span>
                <span class="block font-medium">{{ tenant.name }}</span>
                <span class="text-sm text-muted-foreground">{{ formatTenantRole(tenant.role) }}</span>
              </span>
              <input v-model="selectedTenantId" class="sr-only" type="radio" :value="tenant.id">
              <CheckCircle2 v-if="selectedTenantId === tenant.id" :size="20" class="text-primary" />
            </label>
          </div>

          <div v-if="error" class="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
            {{ error.message }}
            <p v-if="error.traceId" class="mt-2 text-xs text-muted-foreground">
              trace_id: {{ error.traceId }}
            </p>
          </div>

          <Button :disabled="state.loading || !selectedTenantId">
            <Loader2 v-if="state.loading" :size="16" class="mr-2 animate-spin" />
            进入工作台
          </Button>
        </form>
      </CardContent>
    </Card>
  </main>
</template>
